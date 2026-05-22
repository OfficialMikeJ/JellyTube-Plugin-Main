using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API controller for the JellyTube Hype (like) system.
/// </summary>
[ApiController]
[Route("JellyTube/Hype")]
[Authorize(Policy = "DefaultAuthorization")]
public class HypeController : ControllerBase
{
    private readonly ILogger<HypeController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HypeController"/>.
    /// </summary>
    public HypeController(ILogger<HypeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the hype count and the current user's hype state for an item.
    /// GET /JellyTube/Hype/{itemId}
    /// </summary>
    [HttpGet("{itemId:guid}")]
    [ProducesResponseType(typeof(HypeStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<HypeStatusResponse> GetStatus([FromRoute] Guid itemId)
    {
        var userId = GetUserId();
        var status = Plugin.Instance!.HypeService.GetStatus(itemId, userId);
        return Ok(status);
    }

    /// <summary>
    /// Toggles the hype state (hype/un-hype) for the requesting user on an item.
    /// POST /JellyTube/Hype/{itemId}
    /// </summary>
    [HttpPost("{itemId:guid}")]
    [ProducesResponseType(typeof(HypeStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<HypeStatusResponse> Toggle([FromRoute] Guid itemId)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized("Could not determine user identity.");

        Plugin.Instance!.HypeService.ToggleHype(itemId, userId.Value);
        var status = Plugin.Instance.HypeService.GetStatus(itemId, userId.Value);
        return Ok(status);
    }

    /// <summary>
    /// Returns hype counts for a batch of items.
    /// POST /JellyTube/Hype/Batch
    /// Body: array of item GUIDs
    /// </summary>
    [HttpPost("Batch")]
    [ProducesResponseType(typeof(Dictionary<Guid, int>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<Guid, int>> GetBatch([FromBody] List<Guid> itemIds)
    {
        if (itemIds is null || itemIds.Count == 0)
            return BadRequest("Provide at least one item ID.");

        var counts = Plugin.Instance!.HypeService.GetBatchCounts(itemIds);
        return Ok(counts);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("Jellyfin-UserId")?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
