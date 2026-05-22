using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API controller for the JellyTube moderation system.
/// Exposes the content filter, strike management, and account audit endpoints.
/// </summary>
[ApiController]
[Route("JellyTube/Moderation")]
[Authorize(Policy = "DefaultAuthorization")]
public class ModerationController : ControllerBase
{
    private readonly ILogger<ModerationController> _logger;

    /// <summary>Initializes a new instance of <see cref="ModerationController"/>.</summary>
    public ModerationController(ILogger<ModerationController> logger)
    {
        _logger = logger;
    }

    // ── Content filter (called by the web client before posting a comment) ───

    /// <summary>
    /// Runs a comment or message through the server-side spam and toxicity filter.
    /// The web client should call this before submitting any user-generated text.
    /// POST /JellyTube/Moderation/Filter
    /// </summary>
    [HttpPost("Filter")]
    [ProducesResponseType(typeof(ContentFilterResult), StatusCodes.Status200OK)]
    public ActionResult<ContentFilterResult> Filter([FromBody] FilterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
            return Ok(new ContentFilterResult { Blocked = false });

        var result = Plugin.Instance!.ModerationService.Filter(request.Text);

        // If spam, auto-apply a strike silently (system-issued)
        if (result.Blocked && result.Reason == "spam")
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                Plugin.Instance.ModerationService.AddStrike(
                    userId.Value,
                    "Spam detected in comment",
                    "system");
                _logger.LogInformation("Auto-strike for spam applied to user {UserId}", userId.Value);
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Checks whether the authenticated user is allowed to post comments.
    /// GET /JellyTube/Moderation/CanComment
    /// </summary>
    [HttpGet("CanComment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult CanComment()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        bool allowed = Plugin.Instance!.ModerationService.CanComment(userId.Value);
        if (!allowed)
        {
            var record = Plugin.Instance.ModerationService.GetRecord(userId.Value);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                allowed = false,
                reason = record.PermanentlySuspended
                    ? "Your account has been permanently suspended."
                    : $"You are restricted from commenting until {record.CommentBannedUntil:yyyy-MM-dd}."
            });
        }

        return Ok(new { allowed = true });
    }

    // ── Admin-only strike management ─────────────────────────────────────────

    /// <summary>
    /// Returns all tracked user moderation records.
    /// GET /JellyTube/Moderation/Users
    /// </summary>
    [HttpGet("Users")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(List<UserStrikeRecord>), StatusCodes.Status200OK)]
    public ActionResult<List<UserStrikeRecord>> GetAllRecords()
    {
        return Ok(Plugin.Instance!.ModerationService.GetAllRecords());
    }

    /// <summary>
    /// Returns the moderation record for a specific user.
    /// GET /JellyTube/Moderation/Users/{userId}
    /// </summary>
    [HttpGet("Users/{userId:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(UserStrikeRecord), StatusCodes.Status200OK)]
    public ActionResult<UserStrikeRecord> GetRecord([FromRoute] Guid userId)
    {
        return Ok(Plugin.Instance!.ModerationService.GetRecord(userId));
    }

    /// <summary>
    /// Adds a strike to a user. Admin only.
    /// POST /JellyTube/Moderation/Users/{userId}/Strike
    /// </summary>
    [HttpPost("Users/{userId:guid}/Strike")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(UserStrikeRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<UserStrikeRecord> AddStrike(
        [FromRoute] Guid userId,
        [FromQuery] string reason,
        [FromQuery] string? note = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("Reason is required.");

        var record = Plugin.Instance!.ModerationService.AddStrike(userId, reason, "admin", note);
        return Ok(record);
    }

    /// <summary>
    /// Removes a strike from a user. Admin only.
    /// DELETE /JellyTube/Moderation/Users/{userId}/Strike
    /// </summary>
    [HttpDelete("Users/{userId:guid}/Strike")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(UserStrikeRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<UserStrikeRecord> RemoveStrike(
        [FromRoute] Guid userId,
        [FromQuery] string adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return BadRequest("Admin note is required when removing a strike.");

        var record = Plugin.Instance!.ModerationService.RemoveStrike(userId, adminNote);
        return Ok(record);
    }

    /// <summary>
    /// Clears the public review flag on a user account. Admin only.
    /// DELETE /JellyTube/Moderation/Users/{userId}/ReviewFlag
    /// </summary>
    [HttpDelete("Users/{userId:guid}/ReviewFlag")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(UserStrikeRecord), StatusCodes.Status200OK)]
    public ActionResult<UserStrikeRecord> ClearReviewFlag([FromRoute] Guid userId)
    {
        return Ok(Plugin.Instance!.ModerationService.ClearReviewFlag(userId));
    }

    /// <summary>
    /// Unlocks the login ban on a user account without removing the strike. Admin only.
    /// POST /JellyTube/Moderation/Users/{userId}/UnlockLogin
    /// </summary>
    [HttpPost("Users/{userId:guid}/UnlockLogin")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(UserStrikeRecord), StatusCodes.Status200OK)]
    public ActionResult<UserStrikeRecord> UnlockLogin([FromRoute] Guid userId)
    {
        return Ok(Plugin.Instance!.ModerationService.UnlockLogin(userId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("Jellyfin-UserId")?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
