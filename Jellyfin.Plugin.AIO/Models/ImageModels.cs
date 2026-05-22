using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>Persisted store of extended images (banners, large profile pics).</summary>
public class ImageStore
{
    /// <summary>UserId (string) → banner image record.</summary>
    public Dictionary<string, ImageRecord> Banners { get; set; } = new();
}

public class ImageRecord
{
    /// <summary>Absolute server path to the stored image file.</summary>
    public string FilePath    { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long   FileSize    { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ImageUploadResponse
{
    public string Url      { get; set; } = string.Empty;
    public long   FileSize { get; set; }
    public DateTime UpdatedAt { get; set; }
}
