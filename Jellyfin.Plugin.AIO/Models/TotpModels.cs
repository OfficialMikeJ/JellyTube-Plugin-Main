using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>Persisted TOTP secrets store (keyed by Jellyfin user ID).</summary>
public class TotpStore
{
    public Dictionary<string, TotpRecord> Records { get; set; } = new();
}

/// <summary>Per-user TOTP state.</summary>
public class TotpRecord
{
    /// <summary>Base32-encoded TOTP secret.</summary>
    public string Secret    { get; set; } = string.Empty;
    public bool   IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
}

/// <summary>Returned to the client after calling Setup so they can display the QR code.</summary>
public class TotpSetupResponse
{
    /// <summary>Base32 secret — shown as text fallback if QR fails.</summary>
    public string Secret     { get; set; } = string.Empty;
    /// <summary>otpauth:// URI for Google Authenticator / Authy / etc.</summary>
    public string OtpAuthUri { get; set; } = string.Empty;
    /// <summary>Friendly account label shown in the authenticator app.</summary>
    public string Label      { get; set; } = string.Empty;
    public bool   IsEnabled  { get; set; }
}

public class TotpVerifyRequest
{
    /// <summary>6-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

public class TotpStatusResponse
{
    public bool IsEnabled    { get; set; }
    public bool IsConfigured { get; set; }
}
