using System;
using System.IO;
using Jellyfin.Plugin.AIO.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Stores and serves extended creator images: channel banners and high-quality
/// thumbnails.  Images are written to a subdirectory of the Jellyfin data path.
/// </summary>
public class ImageService
{
    private readonly ILogger<ImageService> _logger;
    private readonly string _storePath;
    private readonly string _imageDir;
    private ImageStore _store;
    private readonly object _lock = new();

    public ImageService(IApplicationPaths appPaths, ILogger<ImageService> logger)
    {
        _logger    = logger;
        _imageDir  = Path.Combine(appPaths.DataPath, "jellytube_images");
        _storePath = Path.Combine(appPaths.DataPath, "jellytube_images.json");
        Directory.CreateDirectory(_imageDir);
        _store = Load();
    }

    // ── Banner images ─────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a banner image for the given user, enforcing the configured max size.
    /// Returns the response DTO, or throws <see cref="InvalidOperationException"/> if the size limit is exceeded.
    /// </summary>
    public ImageUploadResponse SaveBanner(Guid userId, Stream imageStream, string contentType, long declaredSize)
    {
        long maxBytes = (Plugin.Instance?.Configuration?.MaxBannerImageSizeMb ?? 5) * 1024L * 1024L;
        if (declaredSize > maxBytes)
            throw new InvalidOperationException($"Banner image exceeds the {maxBytes / 1024 / 1024} MB limit.");

        var ext      = ContentTypeToExt(contentType);
        var fileName = $"banner_{userId:N}{ext}";
        var filePath = Path.Combine(_imageDir, fileName);

        using var fs = File.Create(filePath);
        imageStream.CopyTo(fs);
        long actualSize = fs.Length;

        if (actualSize > maxBytes)
        {
            File.Delete(filePath);
            throw new InvalidOperationException($"Banner image exceeds the {maxBytes / 1024 / 1024} MB limit.");
        }

        var record = new ImageRecord
        {
            FilePath    = filePath,
            ContentType = contentType,
            FileSize    = actualSize,
            UpdatedAt   = DateTime.UtcNow
        };

        lock (_lock)
        {
            _store.Banners[userId.ToString()] = record;
            Save();
        }

        return new ImageUploadResponse
        {
            Url       = $"/JellyTube/Images/{userId}/Banner",
            FileSize  = actualSize,
            UpdatedAt = record.UpdatedAt
        };
    }

    /// <summary>Returns (filePath, contentType) for a user's banner, or null if none.</summary>
    public (string FilePath, string ContentType)? GetBanner(Guid userId)
    {
        lock (_lock)
        {
            if (_store.Banners.TryGetValue(userId.ToString(), out var rec) && File.Exists(rec.FilePath))
                return (rec.FilePath, rec.ContentType);
        }
        return null;
    }

    public void DeleteBanner(Guid userId)
    {
        lock (_lock)
        {
            if (_store.Banners.TryGetValue(userId.ToString(), out var rec))
            {
                if (File.Exists(rec.FilePath)) File.Delete(rec.FilePath);
                _store.Banners.Remove(userId.ToString());
                Save();
            }
        }
    }

    // ── Profile picture (high-res) ────────────────────────────────────────────

    /// <summary>
    /// Saves a high-res profile picture. Returns the file path so the caller can
    /// forward it to Jellyfin's own user-image API if desired.
    /// </summary>
    public ImageUploadResponse SaveProfilePicture(Guid userId, Stream imageStream, string contentType, long declaredSize)
    {
        long maxBytes = (Plugin.Instance?.Configuration?.MaxProfileImageSizeMb ?? 10) * 1024L * 1024L;
        if (declaredSize > maxBytes)
            throw new InvalidOperationException($"Profile image exceeds the {maxBytes / 1024 / 1024} MB limit.");

        var ext      = ContentTypeToExt(contentType);
        var fileName = $"profile_{userId:N}{ext}";
        var filePath = Path.Combine(_imageDir, fileName);

        using var fs = File.Create(filePath);
        imageStream.CopyTo(fs);
        long actualSize = fs.Length;

        if (actualSize > maxBytes)
        {
            File.Delete(filePath);
            throw new InvalidOperationException($"Profile image exceeds the {maxBytes / 1024 / 1024} MB limit.");
        }

        _logger.LogInformation("Saved high-res profile picture for user {UserId} ({Bytes} bytes)", userId, actualSize);
        return new ImageUploadResponse
        {
            Url       = $"/JellyTube/Images/{userId}/ProfilePicture",
            FileSize  = actualSize,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public (string FilePath, string ContentType)? GetProfilePicture(Guid userId)
    {
        var fileName = Directory.GetFiles(_imageDir, $"profile_{userId:N}.*");
        if (fileName.Length == 0) return null;
        var fp  = fileName[0];
        var ct  = ExtToContentType(Path.GetExtension(fp));
        return (fp, ct);
    }

    // ── Thumbnail (high-res per item) ─────────────────────────────────────────

    public ImageUploadResponse SaveThumbnail(Guid itemId, Stream imageStream, string contentType, long declaredSize)
    {
        long maxBytes = (Plugin.Instance?.Configuration?.MaxThumbnailSizeMb ?? 20) * 1024L * 1024L;
        if (declaredSize > maxBytes)
            throw new InvalidOperationException($"Thumbnail exceeds the {maxBytes / 1024 / 1024} MB limit.");

        var ext      = ContentTypeToExt(contentType);
        var fileName = $"thumb_{itemId:N}{ext}";
        var filePath = Path.Combine(_imageDir, fileName);

        using var fs = File.Create(filePath);
        imageStream.CopyTo(fs);
        long actualSize = fs.Length;

        if (actualSize > maxBytes)
        {
            File.Delete(filePath);
            throw new InvalidOperationException($"Thumbnail exceeds the {maxBytes / 1024 / 1024} MB limit.");
        }

        return new ImageUploadResponse
        {
            Url       = $"/JellyTube/Images/Thumbnail/{itemId}",
            FileSize  = actualSize,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public (string FilePath, string ContentType)? GetThumbnail(Guid itemId)
    {
        var files = Directory.GetFiles(_imageDir, $"thumb_{itemId:N}.*");
        if (files.Length == 0) return null;
        return (files[0], ExtToContentType(Path.GetExtension(files[0])));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ContentTypeToExt(string ct) => ct.ToLowerInvariant() switch
    {
        "image/png"  => ".png",
        "image/gif"  => ".gif",
        "image/webp" => ".webp",
        _            => ".jpg"
    };

    private static string ExtToContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".png"  => "image/png",
        ".gif"  => "image/gif",
        ".webp" => "image/webp",
        _       => "image/jpeg"
    };

    // ── Persistence ───────────────────────────────────────────────────────────

    private ImageStore Load()
    {
        if (!File.Exists(_storePath)) return new ImageStore();
        try { return JsonConvert.DeserializeObject<ImageStore>(File.ReadAllText(_storePath)) ?? new(); }
        catch { return new ImageStore(); }
    }

    private void Save() =>
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
}
