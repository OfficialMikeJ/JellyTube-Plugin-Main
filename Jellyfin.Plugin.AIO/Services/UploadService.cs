using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Manages chunked file upload sessions.
/// </summary>
public class UploadService
{
    private readonly ILogger<UploadService> _logger;
    private readonly ConcurrentDictionary<Guid, UploadSession> _sessions = new();

    /// <summary>
    /// Initializes a new instance of <see cref="UploadService"/> with a null logger.
    /// </summary>
    public UploadService() : this(NullLogger<UploadService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="UploadService"/> with a provided logger.
    /// </summary>
    public UploadService(ILogger<UploadService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new upload session.
    /// </summary>
    public InitUploadResponse CreateSession(string fileName, long totalSize)
    {
        var config = Plugin.Instance!.Configuration;

        long maxBytes = config.MaxUploadSizeMb * 1024L * 1024L;
        if (totalSize > maxBytes)
        {
            throw new InvalidOperationException(
                $"File size {totalSize} bytes exceeds the configured maximum of {config.MaxUploadSizeMb} MB.");
        }

        int chunkBytes = config.ChunkSizeMb * 1024 * 1024;
        int totalChunks = (int)Math.Ceiling((double)totalSize / chunkBytes);

        string uploadDir = string.IsNullOrWhiteSpace(config.MediaUploadPath)
            ? Path.GetTempPath()
            : config.MediaUploadPath;

        Directory.CreateDirectory(uploadDir);

        string safeFileName = Path.GetFileName(fileName);
        string tempPath = Path.Combine(uploadDir, $"{Guid.NewGuid()}_{safeFileName}");

        var session = new UploadSession
        {
            FileName = safeFileName,
            TotalSize = totalSize,
            ChunkSize = chunkBytes,
            TotalChunks = totalChunks,
            TempFilePath = tempPath
        };

        _sessions[session.SessionId] = session;
        _logger.LogInformation("Upload session {Id} created for {File} ({Size} bytes, {Chunks} chunks)",
            session.SessionId, safeFileName, totalSize, totalChunks);

        return new InitUploadResponse
        {
            SessionId = session.SessionId,
            ChunkSize = chunkBytes,
            TotalChunks = totalChunks
        };
    }

    /// <summary>
    /// Writes a chunk to the session's temp file.
    /// </summary>
    public async Task<UploadSession> WriteChunkAsync(Guid sessionId, int chunkIndex, Stream data)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Upload session {sessionId} not found.");

        if (session.IsComplete)
            throw new InvalidOperationException("Session is already complete.");

        long offset = (long)chunkIndex * session.ChunkSize;

        await using var fs = new FileStream(
            session.TempFilePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        fs.Seek(offset, SeekOrigin.Begin);
        await data.CopyToAsync(fs).ConfigureAwait(false);

        lock (session.ReceivedChunks)
        {
            session.ReceivedChunks.Add(chunkIndex);
            session.BytesWritten += data.Length > 0 ? data.Length : session.ChunkSize;
        }

        session.LastActivityAt = DateTime.UtcNow;

        _logger.LogDebug("Session {Id}: chunk {Index}/{Total}", sessionId, chunkIndex + 1, session.TotalChunks);
        return session;
    }

    /// <summary>
    /// Marks a session as complete.
    /// </summary>
    public UploadSession CompleteSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Upload session {sessionId} not found.");

        session.IsComplete = true;
        _logger.LogInformation("Upload session {Id} completed: {File}", sessionId, session.FileName);
        return session;
    }

    /// <summary>
    /// Cancels and cleans up a session.
    /// </summary>
    public void CancelSession(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            try { File.Delete(session.TempFilePath); }
            catch { /* best effort */ }
            _logger.LogInformation("Upload session {Id} cancelled.", sessionId);
        }
    }

    /// <summary>
    /// Retrieves a session by ID.
    /// </summary>
    public UploadSession? GetSession(Guid sessionId)
        => _sessions.TryGetValue(sessionId, out var s) ? s : null;
}