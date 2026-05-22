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
/// API controller for the JellySeerr-style media request system.
/// </summary>
[ApiController]
[Route("AIO/Requests")]
[Authorize(Policy = "DefaultAuthorization")]
public class MediaRequestController : ControllerBase
{
    private readonly ILogger<MediaRequestController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MediaRequestController"/>.
    /// </summary>
    public MediaRequestController(ILogger<MediaRequestController> logger)
    {
        _logger = logger;
    }

    // ─── Requests ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all media requests (admin sees all, users see own).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetRequests([FromQuery] bool myOnly = false)
    {
        // Determine user ID from Jellyfin auth claims
        var userIdClaim = User.FindFirst("Jellyfin-UserId")?.Value
                       ?? User.FindFirst("sub")?.Value;

        if (myOnly && Guid.TryParse(userIdClaim, out var uid))
            return Ok(Plugin.Instance!.MediaRequestService.GetByUser(uid));

        return Ok(Plugin.Instance!.MediaRequestService.GetAll());
    }

    /// <summary>
    /// Submits a new media request.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MediaRequest), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult SubmitRequest([FromBody] MediaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");

        var userIdClaim = User.FindFirst("Jellyfin-UserId")?.Value
                       ?? User.FindFirst("sub")?.Value;
        var userNameClaim = User.Identity?.Name ?? "Unknown";

        if (Guid.TryParse(userIdClaim, out var uid))
            request.RequestedByUserId = uid;

        request.RequestedByUsername = userNameClaim;

        var saved = Plugin.Instance!.MediaRequestService.AddRequest(request);
        return CreatedAtAction(nameof(GetRequests), new { }, saved);
    }

    /// <summary>
    /// Updates the status of a request. Admin only.
    /// </summary>
    [HttpPut("{id}/Status")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UpdateStatus(
        [FromRoute] Guid id,
        [FromQuery] RequestStatus status,
        [FromQuery] string? note = null)
    {
        var updated = Plugin.Instance!.MediaRequestService.UpdateStatus(id, status, note);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a request. Admin only.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteRequest([FromRoute] Guid id)
    {
        bool deleted = Plugin.Instance!.MediaRequestService.DeleteRequest(id);
        return deleted ? NoContent() : NotFound();
    }

    // ─── Discovery ────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches TMDb for movies and TV shows.
    /// </summary>
    [HttpGet("/AIO/Discover/Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query is required.");
        var results = await Plugin.Instance!.MediaRequestService.SearchAsync(q).ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Returns currently in-theaters (Released) movies.
    /// </summary>
    [HttpGet("/AIO/Discover/NowPlaying")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> NowPlaying()
    {
        var results = await Plugin.Instance!.MediaRequestService.GetNowPlayingAsync().ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Returns upcoming (Coming Soon) movies.
    /// </summary>
    [HttpGet("/AIO/Discover/Upcoming")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Upcoming()
    {
        var results = await Plugin.Instance!.MediaRequestService.GetUpcomingAsync().ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Returns popular movies.
    /// </summary>
    [HttpGet("/AIO/Discover/PopularMovies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> PopularMovies()
    {
        var results = await Plugin.Instance!.MediaRequestService.GetPopularMoviesAsync().ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Returns popular TV shows.
    /// </summary>
    [HttpGet("/AIO/Discover/PopularTv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> PopularTv()
    {
        var results = await Plugin.Instance!.MediaRequestService.GetPopularTvAsync().ConfigureAwait(false);
        return Ok(results);
    }
}