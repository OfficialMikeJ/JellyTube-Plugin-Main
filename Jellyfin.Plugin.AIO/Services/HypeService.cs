using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Manages the JellyTube Hype (like) system.
/// Data is persisted to a JSON file in the Jellyfin data directory so it
/// survives server restarts.
/// </summary>
public class HypeService
{
    private readonly string _storePath;
    private readonly ILogger<HypeService> _logger;
    private HypeStore _store;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="HypeService"/>.
    /// </summary>
    public HypeService(IApplicationPaths appPaths, ILogger<HypeService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_hypes.json");
        _store = Load();
    }

    // ── Hype toggle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the hype state for a user on an item.
    /// Returns the new hype state for that user.
    /// </summary>
    public bool ToggleHype(Guid itemId, Guid userId)
    {
        lock (_lock)
        {
            if (!_store.ItemHypes.TryGetValue(itemId, out var users))
            {
                users = new HashSet<Guid>();
                _store.ItemHypes[itemId] = users;
            }

            bool nowHyped;
            if (users.Contains(userId))
            {
                users.Remove(userId);
                nowHyped = false;
            }
            else
            {
                users.Add(userId);
                nowHyped = true;
            }

            Save();
            _logger.LogDebug("User {UserId} {Action} item {ItemId}. Total hypes: {Count}",
                userId, nowHyped ? "hyped" : "un-hyped", itemId, users.Count);
            return nowHyped;
        }
    }

    /// <summary>
    /// Returns the hype count and user state for an item.
    /// </summary>
    public HypeStatusResponse GetStatus(Guid itemId, Guid? userId)
    {
        lock (_lock)
        {
            var users = _store.ItemHypes.GetValueOrDefault(itemId) ?? new HashSet<Guid>();
            return new HypeStatusResponse
            {
                ItemId = itemId,
                Count = users.Count,
                UserHyped = userId.HasValue && users.Contains(userId.Value)
            };
        }
    }

    /// <summary>
    /// Returns hype counts for a batch of items.
    /// </summary>
    public Dictionary<Guid, int> GetBatchCounts(IEnumerable<Guid> itemIds)
    {
        lock (_lock)
        {
            var result = new Dictionary<Guid, int>();
            foreach (var id in itemIds)
                result[id] = _store.ItemHypes.TryGetValue(id, out var s) ? s.Count : 0;
            return result;
        }
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private HypeStore Load()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                var json = File.ReadAllText(_storePath);
                return JsonConvert.DeserializeObject<HypeStore>(json) ?? new HypeStore();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load hype store from {Path}. Starting fresh.", _storePath);
        }
        return new HypeStore();
    }

    private void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_store, Formatting.Indented);
            File.WriteAllText(_storePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hype store to {Path}.", _storePath);
        }
    }
}
