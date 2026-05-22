using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.AIO.Models;
using Jellyfin.Plugin.AIO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API controller for chunked file uploads.
/// </summary>
/// <remarks>
/// Upload access requires either:
///   (a) Jellyfin admin role (RequiresElevation), or
///   (b) the user's ID appears in PluginConfiguration.CreatorUserIds.
/// </remarks>
[ApiController]
[Route("AIO/Upload")]
[Authorize(Policy = "DefaultAuthorization")]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="UploadController"/>.
    /// </summary>
    public UploadController(ILogger<UploadController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new chunked upload session.
    /// </summary>
    /// <param name="request">File metadata.</param>
    /// <returns>Session info including chunk size.</returns>
    [HttpPost("Init")]
    [ProducesResponseType(typeof(InitUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<InitUploadResponse> InitSession([FromBody] InitUploadRequest request)
    {
        if (!IsCreatorOrAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Upload access requires creator or admin privileges.");

        if (string.IsNullOrWhiteSpace(request.FileName) || request.TotalSize <= 0)
            return BadRequest("FileName and TotalSize are required.");

        try
        {
            var response = Plugin.Instance!.UploadService.CreateSession(request.FileName, request.TotalSize);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Uploads a single chunk for an existing session.
    /// </summary>
    /// <param name="sessionId">Upload session ID.</param>
    /// <param name="chunkIndex">Zero-based chunk index.</param>
    /// <returns>Updated session progress.</returns>
    [HttpPost("Chunk/{sessionId}/{chunkIndex:int}")]
    [RequestSizeLimit(1_073_741_824)] // 1 GB per chunk — actual limit enforced by ChunkSizeMb config
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadChunk(
        [FromRoute] Guid sessionId,
        [FromRoute] int chunkIndex)
    {
        try
        {
            var session = await Plugin.Instance!.UploadService.WriteChunkAsync(sessionId, chunkIndex, Request.Body)
                .ConfigureAwait(false);

            return Ok(new
            {
                session.SessionId,
                session.ProgressPercent,
                session.ReceivedChunks.Count,
                session.TotalChunks,
                session.IsComplete
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Session {sessionId} not found.");
        }
    }

    /// <summary>
    /// Marks an upload session as complete.
    /// </summary>
    /// <param name="sessionId">Upload session ID.</param>
    /// <returns>Completion confirmation.</returns>
    [HttpPost("Complete/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult CompleteSession([FromRoute] Guid sessionId)
    {
        try
        {
            var session = Plugin.Instance!.UploadService.CompleteSession(sessionId);
            return Ok(new
            {
                session.SessionId,
                session.FileName,
                session.TotalSize,
                session.IsComplete,
                message = "Upload complete. File saved to media library path."
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Session {sessionId} not found.");
        }
    }

    /// <summary>
    /// Cancels an upload session and removes the temporary file.
    /// </summary>
    /// <param name="sessionId">Upload session ID.</param>
    [HttpDelete("Cancel/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult CancelSession([FromRoute] Guid sessionId)
    {
        Plugin.Instance!.UploadService.CancelSession(sessionId);
        return NoContent();
    }

    /// <summary>
    /// Gets the current status of an upload session.
    /// </summary>
    /// <param name="sessionId">Upload session ID.</param>
    [HttpGet("Status/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetStatus([FromRoute] Guid sessionId)
    {
        var session = Plugin.Instance!.UploadService.GetSession(sessionId);
        if (session is null) return NotFound();

        return Ok(new
        {
            session.SessionId,
            session.FileName,
            session.TotalSize,
            session.BytesWritten,
            session.ProgressPercent,
            ReceivedChunks = session.ReceivedChunks.Count,
            session.TotalChunks,
            session.IsComplete,
            session.LastActivityAt
        });
    }

    /// <summary>
    /// Returns the current plugin upload configuration.
    /// </summary>
    [HttpGet("Config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetConfig()
    {
        var cfg = Plugin.Instance!.Configuration;
        return Ok(new
        {
            cfg.MaxUploadSizeMb,
            cfg.ChunkSizeMb,
            cfg.MediaUploadPath
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsCreatorOrAdmin()
    {
        // Jellyfin admins always have access
        if (User.IsInRole("Administrator")) return true;

        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null) return false;

        var userIdClaim = User.FindFirst("Jellyfin-UserId")?.Value
                       ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId)) return false;

        // Check the comma-separated creator allow-list
        foreach (var entry in cfg.CreatorUserIds.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(entry.Trim(), out var allowedId) && allowedId == userId)
                return true;
        }

        return false;
    }
}