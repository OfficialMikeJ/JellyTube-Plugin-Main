using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Tracks the state of an in-progress chunked upload.
/// </summary>
public class UploadSession
{
    /// <summary>Gets or sets the unique session ID.</summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the original file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the total file size in bytes.</summary>
    public long TotalSize { get; set; }

    /// <summary>Gets or sets the chunk size in bytes used for this session.</summary>
    public int ChunkSize { get; set; }

    /// <summary>Gets or sets the total number of chunks expected.</summary>
    public int TotalChunks { get; set; }

    /// <summary>Gets or sets the set of chunk indexes that have been received.</summary>
    public HashSet<int> ReceivedChunks { get; set; } = new();

    /// <summary>Gets or sets the bytes written so far.</summary>
    public long BytesWritten { get; set; }

    /// <summary>Gets or sets the temporary file path on the server.</summary>
    public string TempFilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets when this session was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when this session last received data.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets whether the upload is complete.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Gets the completion percentage.</summary>
    public double ProgressPercent =>
        TotalChunks == 0 ? 0 : Math.Round((double)ReceivedChunks.Count / TotalChunks * 100, 1);
}

/// <summary>
/// DTO for initiating a new upload session.
/// </summary>
public class InitUploadRequest
{
    /// <summary>Gets or sets the file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the total file size in bytes.</summary>
    public long TotalSize { get; set; }
}

/// <summary>
/// Response DTO returned when a session is created.
/// </summary>
public class InitUploadResponse
{
    /// <summary>Gets or sets the session ID.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the chunk size clients should use (bytes).</summary>
    public int ChunkSize { get; set; }

    /// <summary>Gets or sets the total number of chunks.</summary>
    public int TotalChunks { get; set; }
}