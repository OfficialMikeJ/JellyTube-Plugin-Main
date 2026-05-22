using System;
using System.Linq;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Comments API for JellyTube video pages.
/// Posting runs through the content filter automatically — no separate filter call needed.
/// </summary>
[ApiController]
[Route("JellyTube/Comments")]
[Authorize(Policy = "DefaultAuthorization")]
public class CommentController : ControllerBase
{
    private Guid? CurrentUserId()
    {
        var claim = User.FindFirst("Jellyfin-UserId")?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private string CurrentUsername() =>
        User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value ?? "Unknown";

    // ── GET ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all visible comments for a video, newest first (pinned comment always first).
    /// GET /JellyTube/Comments/{itemId}
    /// </summary>
    [HttpGet("{itemId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CommentDto[]), StatusCodes.Status200OK)]
    public ActionResult<CommentDto[]> GetComments([FromRoute] Guid itemId)
    {
        var callerId  = CurrentUserId();
        var isAdmin   = User.IsInRole("Administrator");
        var comments  = Plugin.Instance!.CommentService.GetComments(itemId);

        var dtos = comments.Select(c => new CommentDto
        {
            CommentId  = c.CommentId,
            AuthorName = c.AuthorName,
            Text       = c.Text,
            PostedAt   = c.PostedAt,
            IsPinned   = c.IsPinned,
            Likes      = c.Likes,
            IsOwn      = callerId.HasValue && c.AuthorId == callerId.Value,
            CanDelete  = isAdmin || (callerId.HasValue && c.AuthorId == callerId.Value)
        }).ToArray();

        return Ok(dtos);
    }

    // ── POST ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Posts a new comment. Runs through moderation automatically.
    /// POST /JellyTube/Comments/{itemId}
    /// </summary>
    [HttpPost("{itemId:guid}")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<CommentDto> PostComment([FromRoute] Guid itemId, [FromBody] PostCommentRequest request)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request?.Text) || request.Text.Length > 2000)
            return BadRequest("Comment must be between 1 and 2000 characters.");

        var comment = Plugin.Instance!.CommentService.Post(
            itemId, userId.Value, CurrentUsername(), request.Text, out var rejectReason);

        if (comment is null)
            return BadRequest(rejectReason);

        var dto = new CommentDto
        {
            CommentId  = comment.CommentId,
            AuthorName = comment.AuthorName,
            Text       = comment.Text,
            PostedAt   = comment.PostedAt,
            IsPinned   = false,
            Likes      = 0,
            IsOwn      = true,
            CanDelete  = true
        };

        return CreatedAtAction(nameof(GetComments), new { itemId }, dto);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes a comment. Authors can delete their own; admins can delete any.
    /// DELETE /JellyTube/Comments/{itemId}/{commentId}
    /// </summary>
    [HttpDelete("{itemId:guid}/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteComment([FromRoute] Guid itemId, [FromRoute] Guid commentId)
    {
        var deleted = Plugin.Instance!.CommentService.Delete(itemId, commentId, CurrentUsername());
        return deleted ? NoContent() : NotFound();
    }

    // ── ADMIN ─────────────────────────────────────────────────────────────────

    /// <summary>Pins a comment (admin only). Unpins any previously pinned comment.</summary>
    [HttpPost("{itemId:guid}/{commentId:guid}/Pin")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult PinComment([FromRoute] Guid itemId, [FromRoute] Guid commentId)
    {
        bool ok = Plugin.Instance!.CommentService.Pin(itemId, commentId);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Adds a like to a comment.</summary>
    [HttpPost("{itemId:guid}/{commentId:guid}/Like")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult LikeComment([FromRoute] Guid itemId, [FromRoute] Guid commentId)
    {
        bool ok = Plugin.Instance!.CommentService.Like(itemId, commentId);
        return ok ? NoContent() : NotFound();
    }
}
