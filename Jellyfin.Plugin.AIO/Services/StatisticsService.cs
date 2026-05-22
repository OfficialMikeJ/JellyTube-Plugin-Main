using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Aggregates watch and network statistics from Jellyfin.
/// </summary>
public class StatisticsService
{
    private readonly ISessionManager _sessionManager;
    private readonly IUserManager _userManager;
    private readonly IActivityManager _activityManager;
    private readonly ILogger<StatisticsService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    public StatisticsService(
        ISessionManager sessionManager,
        IUserManager userManager,
        IActivityManager activityManager,
        ILogger<StatisticsService>? logger = null)
    {
        _sessionManager = sessionManager;
        _userManager = userManager;
        _activityManager = activityManager;
        _logger = logger ?? NullLogger<StatisticsService>.Instance;
    }

    /// <summary>
    /// Returns a list of currently active playback sessions.
    /// </summary>
    public List<object> GetActiveSessions()
    {
        var sessions = _sessionManager.Sessions
            .Where(s => s.NowPlayingItem is not null)
            .Select(s => (object)new
            {
                SessionId = s.Id,
                UserName = s.UserName,
                UserId = s.UserId,
                Client = s.Client,
                DeviceName = s.DeviceName,
                RemoteEndPoint = s.RemoteEndPoint,
                NowPlaying = s.NowPlayingItem is null ? null : new
                {
                    Title = s.NowPlayingItem.Name,
                    Type = s.NowPlayingItem.Type.ToString(),
                    s.NowPlayingItem.RunTimeTicks,
                    ProgressTicks = s.PlayState?.PositionTicks,
                    IsPaused = s.PlayState?.IsPaused ?? false
                },
                LastActivity = s.LastActivityDate
            })
            .ToList();

        return sessions;
    }

    /// <summary>
    /// Returns a summary of all users with their aggregate play-event count.
    /// Fetches all user-attributed activity once, then groups in memory.
    /// </summary>
    public async Task<List<object>> GetUserStatsAsync()
    {
        var users = _userManager.Users.ToList();

        // Fetch all user-attributed play events in one call (filtered by HasUserId)
        QueryResult<ActivityLogEntry>? activities = null;
        try
        {
            activities = await _activityManager.GetPagedResultAsync(new ActivityLogQuery
            {
                HasUserId = true,
                MinDate = DateTime.UtcNow.AddYears(-2)
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch activity log.");
        }

        var playEvents = activities?.Items
            .Where(a => a.Type == "VideoPlayback" || a.Type == "AudioPlayback")
            .ToList() ?? new List<ActivityLogEntry>();

        var result = new List<object>();
        foreach (var user in users)
        {
            int count = playEvents.Count(a => a.UserId == user.Id);
            result.Add(new
            {
                UserId = user.Id,
                UserName = user.Username,
                LastLoginDate = user.LastLoginDate,
                LastActivityDate = user.LastActivityDate,
                TotalPlayEvents = count
            });
        }

        return result;
    }

    /// <summary>
    /// Returns a per-weekday breakdown of play events (index 0 = Sunday).
    /// </summary>
    public async Task<int[]> GetWeekdayHeatmapAsync()
    {
        var heatmap = new int[7];

        try
        {
            var activities = await _activityManager.GetPagedResultAsync(new ActivityLogQuery
            {
                HasUserId = true,
                MinDate = DateTime.UtcNow.AddYears(-1)
            }).ConfigureAwait(false);

            foreach (var a in activities.Items.Where(a =>
                a.Type == "VideoPlayback" || a.Type == "AudioPlayback"))
            {
                int day = (int)a.Date.DayOfWeek;
                heatmap[day]++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build weekday heatmap.");
        }

        return heatmap;
    }

    /// <summary>
    /// Returns a simple overview object with top-level counters.
    /// </summary>
    public async Task<object> GetOverviewAsync()
    {
        int activeCount = _sessionManager.Sessions.Count(s => s.NowPlayingItem is not null);
        int userCount = _userManager.Users.Count();
        long totalEvents = 0;

        try
        {
            var activities = await _activityManager.GetPagedResultAsync(
                new ActivityLogQuery()).ConfigureAwait(false);
            totalEvents = activities.TotalRecordCount;
        }
        catch { /* not critical */ }

        return new
        {
            ActiveSessions = activeCount,
            TotalUsers = userCount,
            TotalActivityEvents = totalEvents,
            ServerTime = DateTime.UtcNow
        };
    }
}