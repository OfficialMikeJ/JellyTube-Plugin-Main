using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API controller for the JellyTube A/B thumbnail testing system.
/// </summary>
[ApiController]
[Route("JellyTube/Thumbnails")]
[Authorize(Policy = "DefaultAuthorization")]
public class ThumbnailAbController : ControllerBase
{
    private readonly ILogger<ThumbnailAbController> _logger;

    /// <summary>Initializes a new instance of <see cref="ThumbnailAbController"/>.</summary>
    public ThumbnailAbController(ILogger<ThumbnailAbController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns all thumbnail variants and their CTR stats for a video.
    /// GET /JellyTube/Thumbnails/{itemId}
    /// </summary>
    [HttpGet("{itemId:guid}")]
    [ProducesResponseType(typeof(List<ThumbnailVariant>), StatusCodes.Status200OK)]
    public ActionResult<List<ThumbnailVariant>> GetVariants([FromRoute] Guid itemId)
    {
        return Ok(Plugin.Instance!.ThumbnailAbService.GetVariants(itemId));
    }

    /// <summary>
    /// Returns the next thumbnail variant to display to this viewer (round-robin).
    /// The web client calls this when rendering a video card or watch page.
    /// GET /JellyTube/Thumbnails/{itemId}/Next
    /// </summary>
    [HttpGet("{itemId:guid}/Next")]
    [ProducesResponseType(typeof(ThumbnailVariant), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<ThumbnailVariant> GetNext([FromRoute] Guid itemId)
    {
        var variant = Plugin.Instance!.ThumbnailAbService.GetNextVariant(itemId);
        if (variant is null) return NoContent();
        return Ok(variant);
    }

    /// <summary>
    /// Records a click-through event for a specific variant.
    /// The web client calls this when a viewer clicks a video card.
    /// POST /JellyTube/Thumbnails/{itemId}/Click/{variantId}
    /// </summary>
    [HttpPost("{itemId:guid}/Click/{variantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult RecordClick([FromRoute] Guid itemId, [FromRoute] Guid variantId)
    {
        Plugin.Instance!.ThumbnailAbService.RecordClick(itemId, variantId);
        return NoContent();
    }

    /// <summary>
    /// Adds a new thumbnail variant for a video. Creator/admin only.
    /// POST /JellyTube/Thumbnails/{itemId}
    /// </summary>
    [HttpPost("{itemId:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(ThumbnailVariant), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ThumbnailVariant> AddVariant(
        [FromRoute] Guid itemId,
        [FromBody] AddVariantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.ImagePath))
            return BadRequest("ImagePath is required.");

        try
        {
            var variant = Plugin.Instance!.ThumbnailAbService.AddVariant(
                itemId, request.Label, request.ImagePath);
            return CreatedAtAction(nameof(GetVariants), new { itemId }, variant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes a thumbnail variant. Creator/admin only.
    /// DELETE /JellyTube/Thumbnails/{itemId}/{variantId}
    /// </summary>
    [HttpDelete("{itemId:guid}/{variantId:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult RemoveVariant([FromRoute] Guid itemId, [FromRoute] Guid variantId)
    {
        bool removed = Plugin.Instance!.ThumbnailAbService.RemoveVariant(itemId, variantId);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Manually declares a winning variant. Admin only.
    /// POST /JellyTube/Thumbnails/{itemId}/Winner/{variantId}
    /// </summary>
    [HttpPost("{itemId:guid}/Winner/{variantId:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(typeof(ThumbnailVariant), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ThumbnailVariant> DeclareWinner(
        [FromRoute] Guid itemId,
        [FromRoute] Guid variantId)
    {
        var winner = Plugin.Instance!.ThumbnailAbService.DeclareWinner(itemId, variantId);
        if (winner is null) return NotFound();
        return Ok(winner);
    }
}
