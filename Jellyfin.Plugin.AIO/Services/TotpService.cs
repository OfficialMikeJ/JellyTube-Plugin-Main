using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// RFC 6238 TOTP (Google Authenticator compatible) — implemented in pure .NET,
/// no third-party packages required.  Secrets are stored per Jellyfin user ID.
/// </summary>
public class TotpService
{
    private readonly ILogger<TotpService> _logger;
    private readonly string _storePath;
    private TotpStore _store;
    private readonly object _lock = new();

    // In-memory set of "verified session tokens" (Jellyfin auth token → expiry).
    // Once a user passes TOTP verification their token lives here for 8 hours.
    private readonly ConcurrentDictionary<string, DateTime> _verifiedTokens = new();

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Issuer         = "JellyTube";

    public TotpService(IApplicationPaths appPaths, ILogger<TotpService> logger)
    {
        _logger    = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_totp.json");
        _store     = Load();
    }

    // ── Setup / management ────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new TOTP secret for the user (replaces any existing pending secret)
    /// and returns the setup info (secret + otpauth URI) so the client can show a QR code.
    /// Does NOT enable TOTP — the user must call <see cref="Enable"/> after verifying.
    /// </summary>
    public TotpSetupResponse BeginSetup(Guid userId, string accountName)
    {
        var secret = GenerateSecret();
        lock (_lock)
        {
            var key = userId.ToString();
            if (!_store.Records.TryGetValue(key, out var rec))
                rec = new TotpRecord();

            rec.Secret    = secret;
            rec.IsEnabled = false; // not yet enabled — awaiting verification
            _store.Records[key] = rec;
            Save();
        }

        var uri = BuildOtpAuthUri(accountName, secret);
        return new TotpSetupResponse
        {
            Secret     = secret,
            OtpAuthUri = uri,
            Label      = $"{Issuer}:{accountName}",
            IsEnabled  = false
        };
    }

    /// <summary>Verifies a TOTP code and, if correct, marks TOTP as enabled for the user.</summary>
    public bool Enable(Guid userId, string code)
    {
        var key = userId.ToString();
        TotpRecord? rec;
        lock (_lock) { _store.Records.TryGetValue(key, out rec); }
        if (rec is null || string.IsNullOrWhiteSpace(rec.Secret)) return false;

        if (!VerifyCode(rec.Secret, code)) return false;

        lock (_lock)
        {
            rec.IsEnabled = true;
            rec.EnabledAt = DateTime.UtcNow;
            _store.Records[key] = rec;
            Save();
        }
        return true;
    }

    public void Disable(Guid userId)
    {
        lock (_lock)
        {
            var key = userId.ToString();
            if (_store.Records.TryGetValue(key, out var rec))
            {
                rec.IsEnabled = false;
                rec.EnabledAt = null;
                _store.Records[key] = rec;
                Save();
            }
        }
    }

    public TotpStatusResponse GetStatus(Guid userId)
    {
        var key = userId.ToString();
        lock (_lock)
        {
            if (_store.Records.TryGetValue(key, out var rec))
                return new TotpStatusResponse { IsEnabled = rec.IsEnabled, IsConfigured = !string.IsNullOrEmpty(rec.Secret) };
        }
        return new TotpStatusResponse();
    }

    // ── Session-level verification ────────────────────────────────────────────

    /// <summary>
    /// Verifies a TOTP code for a user who is already authenticated via Jellyfin.
    /// On success, stamps the session token as TOTP-verified for 8 hours.
    /// </summary>
    public bool VerifySession(Guid userId, string code, string jellyfinToken)
    {
        var key = userId.ToString();
        TotpRecord? rec;
        lock (_lock) { _store.Records.TryGetValue(key, out rec); }
        if (rec is null || !rec.IsEnabled) return true; // TOTP not required for this user

        if (!VerifyCode(rec.Secret, code)) return false;

        _verifiedTokens[jellyfinToken] = DateTime.UtcNow.AddHours(8);
        PurgeExpiredTokens();
        return true;
    }

    /// <summary>Returns true if TOTP is not required for the user, or if the session token is already verified.</summary>
    public bool IsSessionVerified(Guid userId, string jellyfinToken)
    {
        var key = userId.ToString();
        TotpRecord? rec;
        lock (_lock) { _store.Records.TryGetValue(key, out rec); }
        if (rec is null || !rec.IsEnabled) return true;

        return _verifiedTokens.TryGetValue(jellyfinToken, out var exp) && exp > DateTime.UtcNow;
    }

    // ── RFC 6238 TOTP core ────────────────────────────────────────────────────

    private static bool VerifyCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;
        byte[] key    = Base32Decode(base32Secret);
        long   window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        // Allow ±1 time step for clock skew
        for (int delta = -1; delta <= 1; delta++)
            if (ComputeTotp(key, window + delta) == code) return true;

        return false;
    }

    private static string ComputeTotp(byte[] key, long counter)
    {
        byte[] msg = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(msg);

        using var hmac = new HMACSHA1(key);
        byte[] hash   = hmac.ComputeHash(msg);

        int offset = hash[^1] & 0x0F;
        int binary  = ((hash[offset]     & 0x7F) << 24)
                    | ((hash[offset + 1] & 0xFF) << 16)
                    | ((hash[offset + 2] & 0xFF) << 8)
                    | ((hash[offset + 3] & 0xFF));

        return (binary % 1_000_000).ToString("D6");
    }

    // ── Base32 ────────────────────────────────────────────────────────────────

    private static string GenerateSecret()
    {
        byte[] bytes = new byte[20]; // 160-bit secret
        RandomNumberGenerator.Fill(bytes);
        return Base32Encode(bytes);
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buf = 0, bitsLeft = 0;
        foreach (byte b in data)
        {
            buf      = (buf << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buf >> bitsLeft) & 31]);
            }
        }
        if (bitsLeft > 0) sb.Append(Base32Alphabet[(buf << (5 - bitsLeft)) & 31]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        base32 = base32.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int buf = 0, bitsLeft = 0;
        foreach (char c in base32)
        {
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0) continue;
            buf      = (buf << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buf >> bitsLeft) & 0xFF));
            }
        }
        return [.. bytes];
    }

    private static string BuildOtpAuthUri(string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits=6&period=30";

    // ── Persistence ───────────────────────────────────────────────────────────

    private TotpStore Load()
    {
        if (!File.Exists(_storePath)) return new TotpStore();
        try { return JsonConvert.DeserializeObject<TotpStore>(File.ReadAllText(_storePath)) ?? new(); }
        catch { return new TotpStore(); }
    }

    private void Save() =>
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));

    private void PurgeExpiredTokens()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _verifiedTokens)
            if (kv.Value < now) _verifiedTokens.TryRemove(kv.Key, out _);
    }
}
