using System;
using Jellyfin.Plugin.AIO.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.AIO;

/// <summary>
/// Injects JellyTube middleware into the ASP.NET Core request pipeline.
/// Registered via <see cref="PluginServiceRegistrator"/> so Jellyfin picks it up automatically.
/// Order: VPN block runs first (drops connection before any auth), then registration rate limiter.
/// </summary>
public class JellyTubeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<VpnBlockMiddleware>();
        app.UseMiddleware<RegistrationRateLimitMiddleware>();
        next(app);
    };
}
