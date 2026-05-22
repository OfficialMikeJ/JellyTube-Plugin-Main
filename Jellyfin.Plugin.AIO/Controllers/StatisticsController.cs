using System.Threading.Tasks;
using Jellyfin.Plugin.AIO.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API controller for the statistics and analytics dashboard.
/// </summary>
[ApiController]
[Route("AIO/Stats")]
[Authorize(Policy = "DefaultAuthorization")]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsService _statsService;
    private readonly ILogger<StatisticsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsController"/>.
    /// </summary>
    public StatisticsController(
        ISessionManager sessionManager,
        IUserManager userManager,
        IActivityManager activityManager,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<StatisticsController>();
        _statsService = new StatisticsService(
            sessionManager,
            userManager,
            activityManager,
            loggerFactory.CreateLogger<StatisticsService>());
    }

    /// <summary>
    /// Returns a high-level overview (active sessions, user count, total events).
    /// </summary>
    [HttpGet("Overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetOverview()
    {
        var overview = await _statsService.GetOverviewAsync().ConfigureAwait(false);
        return Ok(overview);
    }

    /// <summary>
    /// Returns per-user watch statistics.
    /// Admin access only.
    /// </summary>
    [HttpGet("Users")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUserStats()
    {
        var stats = await _statsService.GetUserStatsAsync().ConfigureAwait(false);
        return Ok(stats);
    }

    /// <summary>
    /// Returns a 7-element array (Sun–Sat) of play-event counts.
    /// </summary>
    [HttpGet("Heatmap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetHeatmap()
    {
        var heatmap = await _statsService.GetWeekdayHeatmapAsync().ConfigureAwait(false);
        return Ok(new { days = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }, counts = heatmap });
    }

    /// <summary>
    /// Returns all currently active playback sessions.
    /// </summary>
    [HttpGet("Live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetLiveSessions()
    {
        var sessions = _statsService.GetActiveSessions();
        return Ok(sessions);
    }
}