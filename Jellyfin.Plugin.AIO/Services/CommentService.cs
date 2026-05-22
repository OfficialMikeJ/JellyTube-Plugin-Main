using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Stores and retrieves comments per Jellyfin item.
/// Every post runs through ModerationService — spam is auto-struck, toxicity is rejected.
/// </summary>
public class CommentService
{
    private readonly ILogger<CommentService> _logger;
    private readonly string _storePath;
    private CommentStore _store;
    private readonly object _lock = new();

    public CommentService(IApplicationPaths appPaths, ILogger<CommentService> logger)
    {
        _logger    = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_comments.json");
        _store     = Load();
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Returns visible (non-deleted) comments for an item, pinned first.</summary>
    public List<Comment> GetComments(Guid itemId)
    {
        var key = itemId.ToString();
        lock (_lock)
        {
            if (!_store.ItemComments.TryGetValue(key, out var list)) return new();
            return list
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.PostedAt)
                .ToList();
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Posts a new comment.  Returns null + sets <paramref name="rejectReason"/> if the content
    /// is rejected by the toxicity filter; otherwise returns the saved comment.
    /// Spam auto-strikes the author if configured.
    /// </summary>
    public Comment? Post(Guid itemId, Guid authorId, string authorName, string text, out string rejectReason)
    {
        rejectReason = string.Empty;
        var modSvc = Plugin.Instance?.ModerationService;

        if (modSvc is not null)
        {
            // Block banned commenters
            if (!modSvc.CanComment(authorId))
            {
                rejectReason = ModerationService.FlaggedAccountNotice;
                return null;
            }

            var result = modSvc.Filter(text);

            if (result.Blocked && result.Reason == "toxic")
            {
                rejectReason = ModerationService.ToxicInterceptMessage;
                return null;
            }

            if (result.Blocked && result.Reason == "spam")
            {
                var cfg = Plugin.Instance?.Configuration;
                if (cfg?.AutoStrikeOnSpam == true)
                    modSvc.AddStrike(authorId, "Spam detected in comment", "AutoMod", null);
                rejectReason = "Your comment was flagged as spam and was not posted.";
                return null;
            }
        }

        var comment = new Comment
        {
            ItemId     = itemId,
            AuthorId   = authorId,
            AuthorName = authorName,
            Text       = text.Trim()
        };

        var key = itemId.ToString();
        lock (_lock)
        {
            if (!_store.ItemComments.TryGetValue(key, out var list))
            {
                list = new List<Comment>();
                _store.ItemComments[key] = list;
            }
            list.Add(comment);
            Save();
        }

        _logger.LogInformation("Comment posted on item {ItemId} by {Author}", itemId, authorName);
        return comment;
    }

    // ── Moderation ────────────────────────────────────────────────────────────

    public bool Delete(Guid itemId, Guid commentId, string deletedByUsername)
    {
        var key = itemId.ToString();
        lock (_lock)
        {
            if (!_store.ItemComments.TryGetValue(key, out var list)) return false;
            var c = list.FirstOrDefault(x => x.CommentId == commentId);
            if (c is null) return false;
            c.IsDeleted = true;
            c.DeletedBy = deletedByUsername;
            Save();
            return true;
        }
    }

    public bool Pin(Guid itemId, Guid commentId)
    {
        var key = itemId.ToString();
        lock (_lock)
        {
            if (!_store.ItemComments.TryGetValue(key, out var list)) return false;
            // Unpin all, then pin the selected one
            foreach (var c in list) c.IsPinned = false;
            var target = list.FirstOrDefault(x => x.CommentId == commentId && !x.IsDeleted);
            if (target is null) return false;
            target.IsPinned = true;
            Save();
            return true;
        }
    }

    public bool Like(Guid itemId, Guid commentId)
    {
        var key = itemId.ToString();
        lock (_lock)
        {
            if (!_store.ItemComments.TryGetValue(key, out var list)) return false;
            var c = list.FirstOrDefault(x => x.CommentId == commentId && !x.IsDeleted);
            if (c is null) return false;
            c.Likes++;
            Save();
            return true;
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private CommentStore Load()
    {
        if (!File.Exists(_storePath)) return new CommentStore();
        try { return JsonConvert.DeserializeObject<CommentStore>(File.ReadAllText(_storePath)) ?? new(); }
        catch { return new CommentStore(); }
    }

    private void Save() =>
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
}
