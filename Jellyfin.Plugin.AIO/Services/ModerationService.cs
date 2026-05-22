using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Server-side moderation engine for JellyTube.
/// Handles the deterministic spam/toxicity filter and the three-strike system.
/// Strictly NON-AI — regex + keyword dictionary only.
/// </summary>
public class ModerationService
{
    // ── Constants ────────────────────────────────────────────────────────────

    /// <summary>
    /// The exact intercept message shown to users who post toxic content.
    /// Wording is fixed per spec.
    /// </summary>
    public const string ToxicInterceptMessage =
        "Your message or comment you are trying to post contains material not allowed on the platform, " +
        "please edit or modify your comment before attempting to submit it again.";

    /// <summary>
    /// The exact public/internal notice shown on Strike-2 flagged accounts.
    /// </summary>
    public const string FlaggedAccountNotice =
        "this account is under review for potential spam & or causing user harm";

    // ── Keyword lists ─────────────────────────────────────────────────────────

    private static readonly string[] SpamKeywords =
    {
        "whatsapp", "wh4tsapp", "wh@tsapp", "whts4pp",
        "telegram", "t3legram", "tele_gram",
        "discord", "disc0rd",
        "snapchat", "sn4pchat",
        "bitcoin", "btc", "ethereum", "eth", "crypto", "nft",
        "invest now", "guaranteed profit", "double your money",
        "click here", "click link", "free money", "earn $", "earn money fast",
        "d0 0ne th1ng", "do one thing",
        "dm me", "dm for", "inbox me", "text me",
        "onlyfans", "0nlyfans",
        "subscribe and win", "first 10 people"
    };

    private static readonly string[] ToxicKeywords =
    {
        "idiot", "stupid", "moron", "retard", "retarded", "dumb",
        "loser", "trash", "garbage", "pathetic", "worthless", "useless",
        "shut up", "go die", "kill yourself", "kys",
        "hate you", "you suck", "you stink"
    };

    // Detects http/https/www URLs and common TLD patterns
    private static readonly Regex UrlPattern =
        new Regex(@"(?:https?://|www\.|\b\w+\.\w{2,6}(?:/\S*)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly string _storePath;
    private readonly ILogger<ModerationService> _logger;
    private ModerationStore _store;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ModerationService"/>.
    /// </summary>
    public ModerationService(IApplicationPaths appPaths, ILogger<ModerationService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_moderation.json");
        _store = Load();
    }

    // ── Content filter ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the text through the spam and toxicity filters.
    /// Returns a <see cref="ContentFilterResult"/> indicating whether to block.
    /// </summary>
    public ContentFilterResult Filter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ContentFilterResult { Blocked = false };

        if (IsToxic(text))
        {
            return new ContentFilterResult
            {
                Blocked = true,
                Reason = "toxic",
                Message = ToxicInterceptMessage
            };
        }

        if (IsSpam(text))
        {
            return new ContentFilterResult
            {
                Blocked = true,
                Reason = "spam",
                Message = "Your comment was removed because it was flagged as spam."
            };
        }

        return new ContentFilterResult { Blocked = false };
    }

    private static bool IsToxic(string text)
    {
        var lower = text.ToLowerInvariant();
        return ToxicKeywords.Any(k => lower.Contains(k));
    }

    private static bool IsSpam(string text)
    {
        if (UrlPattern.IsMatch(text)) return true;
        var normalised = NormaliseLeet(text);
        return SpamKeywords.Any(k => normalised.Contains(NormaliseLeet(k)));
    }

    private static string NormaliseLeet(string s)
        => s.ToLowerInvariant()
            .Replace('0', 'o').Replace('1', 'i').Replace('3', 'e')
            .Replace('4', 'a').Replace('5', 's').Replace('7', 't')
            .Replace('@', 'a').Replace('$', 's');

    // ── Strike management ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the strike record for a user. Creates a clean record if none exists.
    /// </summary>
    public UserStrikeRecord GetRecord(Guid userId)
    {
        lock (_lock)
        {
            if (!_store.Records.TryGetValue(userId, out var record))
            {
                record = new UserStrikeRecord { UserId = userId };
                _store.Records[userId] = record;
            }
            return record;
        }
    }

    /// <summary>
    /// Returns all tracked records for the admin dashboard.
    /// </summary>
    public List<UserStrikeRecord> GetAllRecords()
    {
        lock (_lock) { return new List<UserStrikeRecord>(_store.Records.Values); }
    }

    /// <summary>
    /// Adds a strike to a user and applies the corresponding penalty.
    /// Strike 1 → 7-day comment ban.
    /// Strike 2 → Login ban + public review flag.
    /// Strike 3 → Permanent suspension.
    /// </summary>
    public UserStrikeRecord AddStrike(Guid userId, string reason,
        string addedBy = "system", string? adminNote = null)
    {
        lock (_lock)
        {
            var record = GetRecord(userId);
            if (record.PermanentlySuspended) return record;

            record.StrikeCount = Math.Min(record.StrikeCount + 1, 3);
            record.History.Add(new StrikeEvent
            {
                Reason = reason,
                StrikeLevel = record.StrikeCount,
                AddedBy = addedBy,
                AdminNote = adminNote
            });

            switch (record.StrikeCount)
            {
                case 1:
                    record.CommentBannedUntil = DateTime.UtcNow.AddDays(7);
                    break;
                case 2:
                    record.CommentBannedUntil = DateTime.UtcNow.AddDays(365);
                    record.LoginBanned = true;
                    record.FlaggedForReview = true;
                    break;
                case 3:
                    record.PermanentlySuspended = true;
                    record.LoginBanned = true;
                    record.FlaggedForReview = false;
                    break;
            }

            _logger.LogWarning("Strike {Level} applied to user {UserId} by {By}. Reason: {Reason}",
                record.StrikeCount, userId, addedBy, reason);

            Save();
            return record;
        }
    }

    /// <summary>
    /// Removes one strike from a user and lifts the corresponding restrictions.
    /// Requires an admin note.
    /// </summary>
    public UserStrikeRecord RemoveStrike(Guid userId, string adminNote)
    {
        lock (_lock)
        {
            var record = GetRecord(userId);
            record.StrikeCount = Math.Max(record.StrikeCount - 1, 0);
            record.History.Add(new StrikeEvent
            {
                Reason = $"Strike removed by admin.",
                StrikeLevel = record.StrikeCount,
                AddedBy = "admin",
                AdminNote = adminNote
            });

            if (record.StrikeCount < 2)
            {
                record.LoginBanned = false;
                record.FlaggedForReview = false;
            }
            if (record.StrikeCount < 3)
                record.PermanentlySuspended = false;
            if (record.StrikeCount == 0)
                record.CommentBannedUntil = null;

            _logger.LogInformation("Strike removed from user {UserId} by admin. Note: {Note}", userId, adminNote);
            Save();
            return record;
        }
    }

    /// <summary>
    /// Manually clears the review flag (admin action).
    /// </summary>
    public UserStrikeRecord ClearReviewFlag(Guid userId)
    {
        lock (_lock)
        {
            var record = GetRecord(userId);
            record.FlaggedForReview = false;
            Save();
            return record;
        }
    }

    /// <summary>
    /// Manually unlocks a login ban (admin action, does not remove the strike itself).
    /// </summary>
    public UserStrikeRecord UnlockLogin(Guid userId)
    {
        lock (_lock)
        {
            var record = GetRecord(userId);
            if (!record.PermanentlySuspended)
                record.LoginBanned = false;
            Save();
            return record;
        }
    }

    /// <summary>
    /// Returns true if the user is allowed to post comments.
    /// </summary>
    public bool CanComment(Guid userId)
    {
        var r = GetRecord(userId);
        if (r.PermanentlySuspended) return false;
        if (r.CommentBannedUntil.HasValue && r.CommentBannedUntil.Value > DateTime.UtcNow) return false;
        return true;
    }

    /// <summary>
    /// Returns true if the user is allowed to log in.
    /// </summary>
    public bool CanLogin(Guid userId)
    {
        var r = GetRecord(userId);
        return !r.PermanentlySuspended && !r.LoginBanned;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private ModerationStore Load()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                var json = File.ReadAllText(_storePath);
                return JsonConvert.DeserializeObject<ModerationStore>(json) ?? new ModerationStore();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load moderation store. Starting fresh.");
        }
        return new ModerationStore();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist moderation store to {Path}.", _storePath);
        }
    }
}
