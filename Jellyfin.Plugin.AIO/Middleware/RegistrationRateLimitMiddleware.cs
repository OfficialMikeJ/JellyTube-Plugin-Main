using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Middleware;

/// <summary>
/// ASP.NET Core middleware that enforces a registration rate limit.
/// Blocks more than N new account creations per IP within a rolling time window.
/// Default: 5 registrations per IP per 10 minutes.
/// </summary>
public class RegistrationRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RegistrationRateLimitMiddleware> _logger;

    // IP → list of registration timestamps within the current window
    private static readonly ConcurrentDictionary<string, List<DateTime>> _registrationLog = new();
    private static readonly object _cleanupLock = new();
    private static DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of <see cref="RegistrationRateLimitMiddleware"/>.
    /// </summary>
    public RegistrationRateLimitMiddleware(
        RequestDelegate next,
        ILogger<RegistrationRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Middleware invocation — intercepts POST /Users/New and enforces the rate limit.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to the Jellyfin new-user registration endpoint
        if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && context.Request.Path.StartsWithSegments("/Users/New", StringComparison.OrdinalIgnoreCase))
        {
            var cfg = Plugin.Instance?.Configuration;
            if (cfg?.EnableRegistrationRateLimit == true)
            {
                string ip = GetClientIp(context);
                int maxPerWindow = cfg.RegistrationRateLimitMax;
                int windowMinutes = cfg.RegistrationRateLimitWindowMinutes;

                if (IsRateLimited(ip, maxPerWindow, windowMinutes))
                {
                    _logger.LogWarning(
                        "Registration rate limit exceeded for IP {Ip}. Max {Max} per {Window} min.",
                        ip, maxPerWindow, windowMinutes);

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] = (windowMinutes * 60).ToString();
                    await context.Response.WriteAsync(
                        $"Too many account registrations. Maximum {maxPerWindow} accounts " +
                        $"per {windowMinutes} minutes per IP address.")
                        .ConfigureAwait(false);
                    return;
                }

                RecordRegistration(ip);
            }
        }

        await _next(context).ConfigureAwait(false);
        PeriodicCleanup();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsRateLimited(string ip, int max, int windowMinutes)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
        if (!_registrationLog.TryGetValue(ip, out var timestamps)) return false;

        lock (timestamps)
            return timestamps.Count(t => t > cutoff) >= max;
    }

    private static void RecordRegistration(string ip)
    {
        var timestamps = _registrationLog.GetOrAdd(ip, _ => new List<DateTime>());
        lock (timestamps)
            timestamps.Add(DateTime.UtcNow);
    }

    private static string GetClientIp(HttpContext context)
    {
        // Respect X-Forwarded-For if behind a reverse proxy
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void PeriodicCleanup()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(30)) return;
        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(30)) return;
            var cutoff = DateTime.UtcNow.AddHours(-2);
            foreach (var kv in _registrationLog)
            {
                lock (kv.Value)
                    kv.Value.RemoveAll(t => t < cutoff);
            }
            _lastCleanup = DateTime.UtcNow;
        }
    }
}
