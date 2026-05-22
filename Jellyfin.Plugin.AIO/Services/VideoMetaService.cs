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
/// Stores rich video metadata (title, description, tags, category, SwipeShow flag, visibility)
/// that Jellyfin's own library does not support.
/// Key = upload session ID (a GUID string) chosen at upload-init time.
/// </summary>
public class VideoMetaService
{
    private readonly ILogger<VideoMetaService> _logger;
    private readonly string _storePath;
    private VideoMetaStore _store;
    private readonly object _lock = new();

    public VideoMetaService(IApplicationPaths appPaths, ILogger<VideoMetaService> logger)
    {
        _logger    = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_videometa.json");
        _store     = Load();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public VideoMeta Save(SaveVideoMetaRequest req, Guid uploadedBy, string uploadedByName)
    {
        var meta = new VideoMeta
        {
            VideoId        = req.SessionId,
            FileName       = req.FileName,
            Title          = string.IsNullOrWhiteSpace(req.Title) ? req.FileName : req.Title,
            Description    = req.Description,
            Tags           = req.Tags ?? new(),
            Category       = req.Category,
            IsSwipeShow    = req.IsSwipeShow,
            AllowComments  = req.AllowComments,
            Visibility     = req.Visibility,
            ScheduledAt    = req.ScheduledAt,
            UploadedBy     = uploadedBy,
            UploadedByName = uploadedByName,
            UploadedAt     = DateTime.UtcNow
        };

        lock (_lock)
        {
            _store.Videos[req.SessionId] = meta;
            Save();
        }

        _logger.LogInformation("Saved video meta for session {SessionId} — \"{Title}\"", req.SessionId, meta.Title);
        return meta;
    }

    public VideoMeta? Get(string videoId)
    {
        lock (_lock) { return _store.Videos.GetValueOrDefault(videoId); }
    }

    /// <summary>Finds metadata by the associated Jellyfin item ID (set after library scan).</summary>
    public VideoMeta? GetByJellyfinItem(Guid jellyfinItemId)
    {
        lock (_lock)
            return _store.Videos.Values.FirstOrDefault(v => v.JellyfinItemId == jellyfinItemId.ToString());
    }

    public bool AssociateJellyfinItem(string videoId, string jellyfinItemId)
    {
        lock (_lock)
        {
            if (!_store.Videos.TryGetValue(videoId, out var meta)) return false;
            meta.JellyfinItemId = jellyfinItemId;
            Save();
            return true;
        }
    }

    public bool Delete(string videoId)
    {
        lock (_lock)
        {
            if (!_store.Videos.Remove(videoId)) return false;
            Save();
            return true;
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>All public videos, newest first.</summary>
    public List<VideoMeta> GetAll(bool publicOnly = true)
    {
        lock (_lock)
            return _store.Videos.Values
                .Where(v => !publicOnly || v.Visibility == "public")
                .OrderByDescending(v => v.UploadedAt)
                .ToList();
    }

    /// <summary>Videos with SwipeShow enabled.</summary>
    public List<VideoMeta> GetSwipeShow()
    {
        lock (_lock)
            return _store.Videos.Values
                .Where(v => v.IsSwipeShow && v.Visibility == "public")
                .OrderByDescending(v => v.UploadedAt)
                .ToList();
    }

    /// <summary>Videos uploaded by a specific creator.</summary>
    public List<VideoMeta> GetByCreator(Guid creatorId)
    {
        lock (_lock)
            return _store.Videos.Values
                .Where(v => v.UploadedBy == creatorId)
                .OrderByDescending(v => v.UploadedAt)
                .ToList();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private VideoMetaStore Load()
    {
        if (!File.Exists(_storePath)) return new VideoMetaStore();
        try { return JsonConvert.DeserializeObject<VideoMetaStore>(File.ReadAllText(_storePath)) ?? new(); }
        catch { return new VideoMetaStore(); }
    }

    private void Save() =>
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
}
