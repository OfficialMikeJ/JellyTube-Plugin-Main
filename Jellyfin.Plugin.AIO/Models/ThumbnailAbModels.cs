using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Persisted A/B thumbnail store for all items.
/// </summary>
public class ThumbnailAbStore
{
    /// <summary>Maps itemId → list of thumbnail variants.</summary>
    public Dictionary<Guid, List<ThumbnailVariant>> ItemVariants { get; set; } = new();
}

/// <summary>
/// A single thumbnail variant for A/B testing.
/// </summary>
public class ThumbnailVariant
{
    /// <summary>Gets or sets the unique variant ID.</summary>
    public Guid VariantId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the label (e.g. "Variant A", "Variant B").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the server-side path to the thumbnail image.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of times this variant was shown to viewers.</summary>
    public long Impressions { get; set; }

    /// <summary>Gets or sets the number of times viewers clicked through after seeing this variant.</summary>
    public long Clicks { get; set; }

    /// <summary>Gets the click-through rate as a percentage.</summary>
    public double Ctr => Impressions == 0 ? 0 : Math.Round((double)Clicks / Impressions * 100, 2);

    /// <summary>Gets or sets when this variant was added.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets whether this variant has been declared the winner.</summary>
    public bool IsWinner { get; set; }
}

/// <summary>
/// Request body for adding a new thumbnail variant.
/// </summary>
public class AddVariantRequest
{
    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the absolute server path to the image file.</summary>
    public string ImagePath { get; set; } = string.Empty;
}
