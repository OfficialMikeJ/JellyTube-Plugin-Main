using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AIO;

/// <summary>
/// Plugin configuration for JellyTube (Jellyfin AIO).
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    // ── Upload ────────────────────────────────────────────────────────────────

    /// <summary>Absolute path on the server where uploaded files are saved.</summary>
    public string MediaUploadPath { get; set; } = string.Empty;

    /// <summary>Maximum allowed upload size in megabytes.</summary>
    public long MaxUploadSizeMb { get; set; } = 10240; // 10 GB

    /// <summary>Chunk size for chunked uploads in megabytes.</summary>
    public int ChunkSizeMb { get; set; } = 10; // 10 MB

    // ── Media requests ────────────────────────────────────────────────────────

    /// <summary>TMDb API key for media discovery (legacy — JellyTube doesn't use TMDb by default).</summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>Whether the request system requires admin approval.</summary>
    public bool RequireRequestApproval { get; set; } = true;

    /// <summary>Whether anonymous users can view the statistics dashboard.</summary>
    public bool PublicStatistics { get; set; } = false;

    // ── JellyTube: Creator access ─────────────────────────────────────────────

    /// <summary>
    /// Comma-separated list of Jellyfin user IDs that have creator (upload) access.
    /// Leave empty to grant upload access to all admins only.
    /// </summary>
    public string CreatorUserIds { get; set; } = string.Empty;

    // ── JellyTube: Registration rate limiting ─────────────────────────────────

    /// <summary>Enable registration rate limiting to prevent bot account creation.</summary>
    public bool EnableRegistrationRateLimit { get; set; } = true;

    /// <summary>Maximum number of new account registrations per IP per window.</summary>
    public int RegistrationRateLimitMax { get; set; } = 5;

    /// <summary>Time window in minutes for the registration rate limit.</summary>
    public int RegistrationRateLimitWindowMinutes { get; set; } = 10;

    // ── JellyTube: Moderation ─────────────────────────────────────────────────

    /// <summary>Automatically apply a strike when the spam filter detects spam in a comment.</summary>
    public bool AutoStrikeOnSpam { get; set; } = true;

    /// <summary>Extra toxic keywords (comma-separated) loaded at runtime.</summary>
    public string ExtraToxicKeywords { get; set; } = string.Empty;

    // ── JellyTube: VPN block ──────────────────────────────────────────────────

    /// <summary>Block requests from known VPN providers and auto-blacklist their IPs.</summary>
    public bool EnableVpnBlock { get; set; } = true;

    /// <summary>Comma-separated IPs that are always allowed through, even if they match a VPN range.</summary>
    public string VpnWhitelistIps { get; set; } = string.Empty;

    /// <summary>Additional CIDR blocks to treat as VPN (comma-separated, e.g. "1.2.3.0/24,5.6.7.0/22").</summary>
    public string VpnCustomCidrBlocks { get; set; } = string.Empty;

    // ── JellyTube: UI preferences ─────────────────────────────────────────────

    /// <summary>When true, viewers cannot see the subscriber/user count.</summary>
    public bool HideSubscriberCount { get; set; } = false;

    // ── JellyTube: Image size limits ──────────────────────────────────────────

    /// <summary>Maximum profile picture upload size in MB for creator/admin accounts.</summary>
    public int MaxProfileImageSizeMb { get; set; } = 10;

    /// <summary>Maximum channel banner image size in MB for creator/admin accounts.</summary>
    public int MaxBannerImageSizeMb { get; set; } = 5;

    /// <summary>Maximum video thumbnail size in MB for creator/admin accounts.</summary>
    public int MaxThumbnailSizeMb { get; set; } = 20;
}