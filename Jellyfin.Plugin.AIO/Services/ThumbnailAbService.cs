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
/// Manages A/B thumbnail testing for JellyTube.
/// Rotates variants round-robin to viewers and tracks impressions + clicks
/// to determine a winning thumbnail automatically.
/// </summary>
public class ThumbnailAbService
{
    private readonly string _storePath;
    private readonly ILogger<ThumbnailAbService> _logger;
    private ThumbnailAbStore _store;
    private readonly object _lock = new();

    // Tracks the next variant index to serve per item (round-robin pointer)
    private readonly Dictionary<Guid, int> _roundRobinPointer = new();

    /// <summary>Initializes a new instance of <see cref="ThumbnailAbService"/>.</summary>
    public ThumbnailAbService(IApplicationPaths appPaths, ILogger<ThumbnailAbService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_thumbnailab.json");
        _store = Load();
    }

    // ── Variant management ────────────────────────────────────────────────────

    /// <summary>
    /// Returns all variants registered for an item.
    /// </summary>
    public List<ThumbnailVariant> GetVariants(Guid itemId)
    {
        lock (_lock)
            return _store.ItemVariants.TryGetValue(itemId, out var v) ? new List<ThumbnailVariant>(v) : new();
    }

    /// <summary>
    /// Adds a new thumbnail variant for an item. Maximum of 4 variants allowed.
    /// </summary>
    public ThumbnailVariant AddVariant(Guid itemId, string label, string imagePath)
    {
        lock (_lock)
        {
            if (!_store.ItemVariants.TryGetValue(itemId, out var variants))
            {
                variants = new List<ThumbnailVariant>();
                _store.ItemVariants[itemId] = variants;
            }

            if (variants.Count >= 4)
                throw new InvalidOperationException("Maximum of 4 thumbnail variants per video.");

            var variant = new ThumbnailVariant
            {
                Label = string.IsNullOrWhiteSpace(label)
                    ? $"Variant {(char)('A' + variants.Count)}"
                    : label,
                ImagePath = imagePath
            };

            variants.Add(variant);
            Save();
            _logger.LogInformation("Added A/B variant {Label} for item {ItemId}", variant.Label, itemId);
            return variant;
        }
    }

    /// <summary>
    /// Removes a specific variant. Cannot remove the winner.
    /// </summary>
    public bool RemoveVariant(Guid itemId, Guid variantId)
    {
        lock (_lock)
        {
            if (!_store.ItemVariants.TryGetValue(itemId, out var variants)) return false;
            var variant = variants.FirstOrDefault(v => v.VariantId == variantId);
            if (variant is null || variant.IsWinner) return false;

            variants.Remove(variant);
            Save();
            return true;
        }
    }

    // ── Round-robin serving ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the next variant to show a viewer (round-robin rotation)
    /// and increments its impression count.
    /// If a winner has been declared, always returns the winner.
    /// Returns null if no variants are configured.
    /// </summary>
    public ThumbnailVariant? GetNextVariant(Guid itemId)
    {
        lock (_lock)
        {
            if (!_store.ItemVariants.TryGetValue(itemId, out var variants) || variants.Count == 0)
                return null;

            // Always show winner once declared
            var winner = variants.FirstOrDefault(v => v.IsWinner);
            if (winner is not null)
            {
                winner.Impressions++;
                Save();
                return winner;
            }

            // Round-robin
            if (!_roundRobinPointer.TryGetValue(itemId, out int idx)) idx = 0;
            idx = idx % variants.Count;
            _roundRobinPointer[itemId] = (idx + 1) % variants.Count;

            variants[idx].Impressions++;
            Save();
            return variants[idx];
        }
    }

    // ── CTR tracking ──────────────────────────────────────────────────────────

    /// <summary>
    /// Records a click-through for a specific variant.
    /// Automatically declares a winner when one variant reaches
    /// statistical significance (≥100 impressions each and 20% higher CTR).
    /// </summary>
    public void RecordClick(Guid itemId, Guid variantId)
    {
        lock (_lock)
        {
            if (!_store.ItemVariants.TryGetValue(itemId, out var variants)) return;
            var variant = variants.FirstOrDefault(v => v.VariantId == variantId);
            if (variant is null) return;

            variant.Clicks++;
            _logger.LogDebug("Click recorded for variant {Variant} on item {ItemId}", variant.Label, itemId);

            TryDeclareWinner(itemId, variants);
            Save();
        }
    }

    /// <summary>
    /// Manually declares a winner for a test. Admin action.
    /// </summary>
    public ThumbnailVariant? DeclareWinner(Guid itemId, Guid variantId)
    {
        lock (_lock)
        {
            if (!_store.ItemVariants.TryGetValue(itemId, out var variants)) return null;
            foreach (var v in variants) v.IsWinner = false;
            var winner = variants.FirstOrDefault(v => v.VariantId == variantId);
            if (winner is not null) winner.IsWinner = true;
            Save();
            return winner;
        }
    }

    // ── Automatic winner detection ────────────────────────────────────────────

    private static void TryDeclareWinner(Guid itemId, List<ThumbnailVariant> variants)
    {
        if (variants.Any(v => v.IsWinner)) return;
        if (variants.Count < 2) return;

        // All variants need at least 100 impressions before we declare
        if (variants.Any(v => v.Impressions < 100)) return;

        var best = variants.OrderByDescending(v => v.Ctr).First();
        var secondBest = variants.OrderByDescending(v => v.Ctr).Skip(1).First();

        // Declare winner if best CTR is at least 20% higher than next best
        if (best.Impressions > 0 && secondBest.Impressions > 0
            && best.Ctr >= secondBest.Ctr * 1.20)
        {
            best.IsWinner = true;
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private ThumbnailAbStore Load()
    {
        try
        {
            if (File.Exists(_storePath))
                return JsonConvert.DeserializeObject<ThumbnailAbStore>(
                    File.ReadAllText(_storePath)) ?? new ThumbnailAbStore();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load A/B thumbnail store. Starting fresh.");
        }
        return new ThumbnailAbStore();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist A/B thumbnail store.");
        }
    }
}
