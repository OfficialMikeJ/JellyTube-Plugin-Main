using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>Persisted VPN / IP-blacklist store.</summary>
public class VpnBlockStore
{
    /// <summary>IP (string) → blacklist entry. Also used for manual whitelist entries.</summary>
    public Dictionary<string, VpnBlockEntry> Entries { get; set; } = new();
}

/// <summary>A single blacklist or whitelist IP entry.</summary>
public class VpnBlockEntry
{
    public bool IsWhitelisted  { get; set; }
    public string Reason       { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Response returned by the admin list endpoint.</summary>
public class VpnBlockListResponse
{
    public List<VpnBlockEntryDto> Blacklisted { get; set; } = new();
    public List<VpnBlockEntryDto> Whitelisted { get; set; } = new();
}

public class VpnBlockEntryDto
{
    public string Ip           { get; set; } = string.Empty;
    public string Reason       { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
