using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Stores and retrieves JellyTube video metadata (title, description, tags, category,
/// SwipeShow flag, visibility) that Jellyfin's library does not natively support.
/// </summary>
[ApiController]
[Route("JellyTube/VideoMeta")]
[Authorize(Policy = "DefaultAuthorization")]
public class VideoMetaController : ControllerBase
{
    private Guid? CurrentUserId()
    {
        var claim = User.FindFirst("Jellyfin-UserId")?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsCreatorOrAdmin()
    {
        if (User.IsInRole("Administrator")) return true;
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null) return false;
        var claim = User.FindFirst("Jellyfin-UserId")?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(claim, out var userId)) return false;
        foreach (var e in cfg.CreatorUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(e.Trim(), out var id) && id == userId) return true;
        return false;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// All public videos, newest first.  Pass ?creatorId=… to filter by uploader.
    /// GET /JellyTube/VideoMeta
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<VideoMeta>), StatusCodes.Status200OK)]
    public ActionResult<List<VideoMeta>> GetAll([FromQuery] Guid? creatorId)
    {
        var svc = Plugin.Instance!.VideoMetaService;
        var results = creatorId.HasValue
            ? svc.GetByCreator(creatorId.Value)
            : svc.GetAll(publicOnly: !User.IsInRole("Administrator"));
        return Ok(results);
    }

    /// <summary>
    /// Videos with SwipeShow enabled (used by the SwipeShow page).
    /// GET /JellyTube/VideoMeta/SwipeShow
    /// </summary>
    [HttpGet("SwipeShow")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<VideoMeta>), StatusCodes.Status200OK)]
    public ActionResult<List<VideoMeta>> GetSwipeShow() =>
        Ok(Plugin.Instance!.VideoMetaService.GetSwipeShow());

    /// <summary>
    /// Metadata for a single video by JellyTube video ID.
    /// GET /JellyTube/VideoMeta/{videoId}
    /// </summary>
    [HttpGet("{videoId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VideoMeta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<VideoMeta> Get([FromRoute] string videoId)
    {
        var meta = Plugin.Instance!.VideoMetaService.Get(videoId);
        if (meta is null) return NotFound();
        return Ok(meta);
    }

    /// <summary>
    /// Metadata by Jellyfin library item ID.
    /// GET /JellyTube/VideoMeta/ByItem/{jellyfinItemId}
    /// </summary>
    [HttpGet("ByItem/{jellyfinItemId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VideoMeta), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<VideoMeta> GetByItem([FromRoute] Guid jellyfinItemId)
    {
        var meta = Plugin.Instance!.VideoMetaService.GetByJellyfinItem(jellyfinItemId);
        if (meta is null) return NotFound();
        return Ok(meta);
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves metadata for an uploaded video. Called by the upload wizard after
    /// POST /AIO/Upload/Complete completes successfully.
    /// POST /JellyTube/VideoMeta
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VideoMeta), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<VideoMeta> Save([FromBody] SaveVideoMetaRequest request)
    {
        if (!IsCreatorOrAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Creator or admin access required.");

        if (string.IsNullOrWhiteSpace(request?.SessionId))
            return BadRequest("SessionId is required.");

        var userId   = CurrentUserId() ?? Guid.Empty;
        var userName = User.FindFirst("name")?.Value ?? "Unknown";

        var meta = Plugin.Instance!.VideoMetaService.Save(request, userId, userName);
        return CreatedAtAction(nameof(Get), new { videoId = meta.VideoId }, meta);
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Associates a JellyTube video ID with a Jellyfin library item ID.
    /// Called after triggering a library scan and getting the Jellyfin item ID.
    /// POST /JellyTube/VideoMeta/{videoId}/Associate
    /// </summary>
    [HttpPost("{videoId}/Associate")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Associate([FromRoute] string videoId, [FromBody] AssociateJellyfinItemRequest request)
    {
        bool ok = Plugin.Instance!.VideoMetaService.AssociateJellyfinItem(videoId, request.JellyfinItemId);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Deletes video metadata (admin only).</summary>
    [HttpDelete("{videoId}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Delete([FromRoute] string videoId)
    {
        Plugin.Instance!.VideoMetaService.Delete(videoId);
        return NoContent();
    }
}
