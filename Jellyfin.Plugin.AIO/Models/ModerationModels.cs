using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Persisted moderation store for all users.
/// </summary>
public class ModerationStore
{
    /// <summary>Maps userId → their strike record.</summary>
    public Dictionary<Guid, UserStrikeRecord> Records { get; set; } = new();
}

/// <summary>
/// Strike record for a single user.
/// </summary>
public class UserStrikeRecord
{
    /// <summary>Gets or sets the Jellyfin user ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the number of active strikes (0–3).</summary>
    public int StrikeCount { get; set; }

    /// <summary>Gets or sets when the comment ban expires (null if not banned).</summary>
    public DateTime? CommentBannedUntil { get; set; }

    /// <summary>Gets or sets whether the user is locked out of login.</summary>
    public bool LoginBanned { get; set; }

    /// <summary>Gets or sets whether this account is publicly flagged for review.</summary>
    public bool FlaggedForReview { get; set; }

    /// <summary>Gets or sets whether the account is permanently suspended.</summary>
    public bool PermanentlySuspended { get; set; }

    /// <summary>Gets or sets the audit trail of strike events.</summary>
    public List<StrikeEvent> History { get; set; } = new();
}

/// <summary>
/// A single entry in the strike audit trail.
/// </summary>
public class StrikeEvent
{
    /// <summary>Gets or sets when the event occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the reason for the strike.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the strike level after this event.</summary>
    public int StrikeLevel { get; set; }

    /// <summary>Gets or sets who issued the strike: "system" or "admin".</summary>
    public string AddedBy { get; set; } = "system";

    /// <summary>Gets or sets an optional admin note.</summary>
    public string? AdminNote { get; set; }
}

/// <summary>
/// Result of the server-side content filter.
/// </summary>
public class ContentFilterResult
{
    /// <summary>Gets or sets whether the content is blocked.</summary>
    public bool Blocked { get; set; }

    /// <summary>Gets or sets the reason: "spam", "toxic", or null.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the message to show the user (if blocked and toxic).</summary>
    public string? Message { get; set; }
}

/// <summary>
/// Request body for the comment filter endpoint.
/// </summary>
public class FilterRequest
{
    /// <summary>Gets or sets the text to filter.</summary>
    public string Text { get; set; } = string.Empty;
}
