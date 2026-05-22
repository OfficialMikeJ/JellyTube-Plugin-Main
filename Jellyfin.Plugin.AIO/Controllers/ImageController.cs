using System;
using System.IO;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Extended image upload/serve endpoints for creator accounts.
/// Supports high-res profile pictures, channel banners, and video thumbnails —
/// all with configurable size limits larger than Jellyfin's defaults.
/// </summary>
[ApiController]
[Route("JellyTube/Images")]
[Authorize(Policy = "DefaultAuthorization")]
public class ImageController : ControllerBase
{
    private bool IsCreatorOrAdmin()
    {
        if (User.IsInRole("Administrator")) return true;
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null) return false;
        var claim = User.FindFirst("Jellyfin-UserId")?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(claim, out var userId)) return false;
        foreach (var entry in cfg.CreatorUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(entry.Trim(), out var allowed) && allowed == userId) return true;
        return false;
    }

    // ── Banner ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upload a channel banner image (creator/admin only).
    /// POST /JellyTube/Images/{userId}/Banner
    /// Body: raw image bytes.  Content-Type must be image/*.
    /// Header: X-JT-FileSize: {bytes} (optional — used for pre-flight size check).
    /// </summary>
    [HttpPost("{userId:guid}/Banner")]
    [RequestSizeLimit(52_428_800)] // 50 MB hard cap at transport layer
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ImageUploadResponse> UploadBanner([FromRoute] Guid userId)
    {
        if (!IsCreatorOrAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Creator or admin access required.");

        var ct           = Request.ContentType ?? "image/jpeg";
        long declared    = Request.ContentLength ?? 0;

        try
        {
            var result = Plugin.Instance!.ImageService.SaveBanner(userId, Request.Body, ct, declared);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Retrieve a user's channel banner.
    /// GET /JellyTube/Images/{userId}/Banner
    /// </summary>
    [HttpGet("{userId:guid}/Banner")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetBanner([FromRoute] Guid userId)
    {
        var result = Plugin.Instance!.ImageService.GetBanner(userId);
        if (result is null) return NotFound();
        return PhysicalFile(result.Value.FilePath, result.Value.ContentType);
    }

    /// <summary>
    /// Delete a user's channel banner (admin only).
    /// DELETE /JellyTube/Images/{userId}/Banner
    /// </summary>
    [HttpDelete("{userId:guid}/Banner")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult DeleteBanner([FromRoute] Guid userId)
    {
        Plugin.Instance!.ImageService.DeleteBanner(userId);
        return NoContent();
    }

    // ── High-res profile picture ──────────────────────────────────────────────

    /// <summary>
    /// Upload a high-resolution profile picture (creator/admin only).
    /// POST /JellyTube/Images/{userId}/ProfilePicture
    /// </summary>
    [HttpPost("{userId:guid}/ProfilePicture")]
    [RequestSizeLimit(20_971_520)] // 20 MB
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ImageUploadResponse> UploadProfilePicture([FromRoute] Guid userId)
    {
        if (!IsCreatorOrAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Creator or admin access required.");

        try
        {
            var ct     = Request.ContentType ?? "image/jpeg";
            var result = Plugin.Instance!.ImageService.SaveProfilePicture(userId, Request.Body, ct, Request.ContentLength ?? 0);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Retrieve a high-res profile picture served by JellyTube.
    /// GET /JellyTube/Images/{userId}/ProfilePicture
    /// </summary>
    [HttpGet("{userId:guid}/ProfilePicture")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetProfilePicture([FromRoute] Guid userId)
    {
        var result = Plugin.Instance!.ImageService.GetProfilePicture(userId);
        if (result is null) return NotFound();
        return PhysicalFile(result.Value.FilePath, result.Value.ContentType);
    }

    // ── High-res thumbnail ────────────────────────────────────────────────────

    /// <summary>
    /// Upload a high-resolution video thumbnail (creator/admin only).
    /// POST /JellyTube/Images/Thumbnail/{itemId}
    /// </summary>
    [HttpPost("Thumbnail/{itemId:guid}")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ImageUploadResponse> UploadThumbnail([FromRoute] Guid itemId)
    {
        if (!IsCreatorOrAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, "Creator or admin access required.");

        try
        {
            var ct     = Request.ContentType ?? "image/jpeg";
            var result = Plugin.Instance!.ImageService.SaveThumbnail(itemId, Request.Body, ct, Request.ContentLength ?? 0);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Retrieve a high-res video thumbnail.
    /// GET /JellyTube/Images/Thumbnail/{itemId}
    /// </summary>
    [HttpGet("Thumbnail/{itemId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetThumbnail([FromRoute] Guid itemId)
    {
        var result = Plugin.Instance!.ImageService.GetThumbnail(itemId);
        if (result is null) return NotFound();
        return PhysicalFile(result.Value.FilePath, result.Value.ContentType);
    }
}
