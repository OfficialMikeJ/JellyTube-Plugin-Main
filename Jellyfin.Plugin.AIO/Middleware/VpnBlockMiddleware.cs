using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AIO.Middleware;

/// <summary>
/// ASP.NET Core middleware that blocks requests from known VPN providers and
/// auto-blacklists their IPs.  Whitelisted IPs always pass through.
/// Controlled by <see cref="PluginConfiguration.EnableVpnBlock"/>.
/// </summary>
public class VpnBlockMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VpnBlockMiddleware> _logger;

    public VpnBlockMiddleware(RequestDelegate next, ILogger<VpnBlockMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg?.EnableVpnBlock == true)
        {
            string ip = GetClientIp(context);

            if (Plugin.Instance!.VpnBlockService.IsBlocked(ip))
            {
                _logger.LogWarning("VPN block: blocked request from {Ip} to {Path}", ip, context.Request.Path);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                await context.Response.WriteAsync(
                    "Access denied: VPN and proxy connections are not permitted on this server.")
                    .ConfigureAwait(false);
                return;
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
