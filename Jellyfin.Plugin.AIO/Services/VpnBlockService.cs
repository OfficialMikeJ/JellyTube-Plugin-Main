using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Checks incoming IP addresses against known VPN/datacenter CIDR ranges and a
/// persistent manual blacklist/whitelist.  Auto-blacklists IPs on first VPN hit.
/// </summary>
public class VpnBlockService
{
    private readonly ILogger<VpnBlockService> _logger;
    private readonly string _storePath;
    private VpnBlockStore _store;
    private readonly object _lock = new();

    // ── Known VPN / datacenter CIDR blocks ───────────────────────────────────
    // Format: (network address as uint, mask bits)
    private static readonly (uint Net, int Bits)[] KnownVpnCidrs = BuildKnownList();

    private static (uint Net, int Bits)[] BuildKnownList()
    {
        // Major VPN providers and their confirmed IP allocations.
        // Admins can add extra CIDR blocks via plugin config (VpnCustomCidrBlocks).
        string[] ranges =
        [
            // NordVPN / Tefincom
            "5.253.204.0/22",
            "82.102.14.0/23",
            "82.102.16.0/23",
            "146.70.0.0/15",
            "185.230.124.0/22",
            // M247 (hosts NordVPN, ExpressVPN, and others)
            "37.120.130.0/24",
            "37.120.131.0/24",
            "77.81.96.0/20",
            "185.216.32.0/22",
            "195.206.96.0/19",
            // ExpressVPN
            "185.166.140.0/22",
            "185.212.170.0/23",
            // Mullvad VPN
            "45.83.220.0/22",
            "91.90.44.0/22",
            "185.156.44.0/22",
            "185.213.154.0/23",
            "193.138.195.0/24",
            // Private Internet Access (PIA)
            "198.54.128.0/17",
            "209.222.0.0/22",
            // Surfshark
            "169.150.196.0/23",
            "185.65.134.0/23",
            "185.65.136.0/22",
            // ProtonVPN
            "37.19.198.0/23",
            "185.107.80.0/22",
            "185.159.156.0/22",
            "185.250.201.0/24",
            // Windscribe
            "149.88.104.0/22",
            "185.242.4.0/22",
            // IPVanish
            "66.235.168.0/21",
            "173.199.64.0/18",
            // CyberGhost
            "91.208.64.0/18",
            "93.190.76.0/22",
            // HideMyAss (HMA)
            "82.145.61.0/24",
            "185.253.96.0/22",
            // TunnelBear
            "216.21.168.0/21",
            // TorGuard
            "104.200.128.0/17",
            "173.212.192.0/18",
            // IVPN
            "188.214.122.0/23",
            "194.165.16.0/23",
            // Hide.me
            "185.186.56.0/22",
            "194.116.80.0/20",
            // Frantech / BuyVM (popular anonymous hosting)
            "107.189.0.0/16",
            "192.46.0.0/19",
            // Choopa / Vultr (datacenter used heavily by VPN exit nodes)
            "45.32.0.0/15",
            "104.238.128.0/17",
            "108.61.128.0/17",
            "149.28.0.0/15",
            // QuadraNet
            "154.47.0.0/16",
            // DataCamp Limited
            "45.152.64.0/18",
            "91.204.72.0/22",
            "185.130.44.0/22",
        ];

        var list = new List<(uint, int)>(ranges.Length);
        foreach (var cidr in ranges)
        {
            var parsed = ParseCidr(cidr);
            if (parsed.HasValue) list.Add(parsed.Value);
        }
        return list.ToArray();
    }

    public VpnBlockService(IApplicationPaths appPaths, ILogger<VpnBlockService> logger)
    {
        _logger   = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_vpnblock.json");
        _store    = Load();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="ip"/> should be blocked.
    /// Whitelisted IPs always pass.  Known-VPN IPs are auto-blacklisted on first hit.
    /// </summary>
    public bool IsBlocked(string ip)
    {
        lock (_lock)
        {
            if (_store.Entries.TryGetValue(ip, out var entry))
            {
                if (entry.IsWhitelisted) return false;
                return true; // explicitly blacklisted
            }
        }

        if (!IsVpnIp(ip)) return false;

        // First detection — auto-blacklist and persist
        Blacklist(ip, "Auto-detected VPN/datacenter IP");
        _logger.LogWarning("VPN block: auto-blacklisted {Ip}", ip);
        return true;
    }

    public void Blacklist(string ip, string reason = "Manual")
    {
        lock (_lock)
        {
            _store.Entries[ip] = new VpnBlockEntry { IsWhitelisted = false, Reason = reason };
            Save();
        }
    }

    public void Whitelist(string ip)
    {
        lock (_lock)
        {
            _store.Entries[ip] = new VpnBlockEntry { IsWhitelisted = true, Reason = "Manual whitelist" };
            Save();
        }
    }

    public void Remove(string ip)
    {
        lock (_lock)
        {
            _store.Entries.Remove(ip);
            Save();
        }
    }

    public VpnBlockListResponse GetList() =>
        new()
        {
            Blacklisted = _store.Entries
                .Where(kv => !kv.Value.IsWhitelisted)
                .Select(kv => new VpnBlockEntryDto { Ip = kv.Key, Reason = kv.Value.Reason, RecordedAt = kv.Value.RecordedAt })
                .OrderByDescending(e => e.RecordedAt).ToList(),
            Whitelisted = _store.Entries
                .Where(kv => kv.Value.IsWhitelisted)
                .Select(kv => new VpnBlockEntryDto { Ip = kv.Key, Reason = kv.Value.Reason, RecordedAt = kv.Value.RecordedAt })
                .OrderByDescending(e => e.RecordedAt).ToList()
        };

    // ── CIDR matching ─────────────────────────────────────────────────────────

    private bool IsVpnIp(string ipStr)
    {
        if (!IPAddress.TryParse(ipStr, out var addr)) return false;
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false; // IPv6 not covered

        uint ip = IpToUint(addr);

        // Check plugin-configured extra CIDR blocks first
        var cfg = Plugin.Instance?.Configuration;
        if (!string.IsNullOrWhiteSpace(cfg?.VpnCustomCidrBlocks))
        {
            foreach (var extra in cfg.VpnCustomCidrBlocks.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parsed = ParseCidr(extra.Trim());
                if (parsed.HasValue && CidrContains(ip, parsed.Value.Net, parsed.Value.Bits))
                    return true;
            }
        }

        foreach (var (net, bits) in KnownVpnCidrs)
            if (CidrContains(ip, net, bits)) return true;

        return false;
    }

    private static bool CidrContains(uint ip, uint network, int bits)
    {
        if (bits == 0) return true;
        uint mask = bits == 32 ? 0xFFFFFFFF : ~(0xFFFFFFFF >> bits);
        return (ip & mask) == (network & mask);
    }

    private static uint IpToUint(IPAddress addr)
    {
        byte[] b = addr.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static (uint Net, int Bits)? ParseCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return null;
        if (!IPAddress.TryParse(parts[0], out var addr)) return null;
        if (!int.TryParse(parts[1], out var bits) || bits < 0 || bits > 32) return null;
        return (IpToUint(addr), bits);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private VpnBlockStore Load()
    {
        if (!File.Exists(_storePath)) return new VpnBlockStore();
        try { return JsonConvert.DeserializeObject<VpnBlockStore>(File.ReadAllText(_storePath)) ?? new(); }
        catch { return new VpnBlockStore(); }
    }

    private void Save() =>
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
}
