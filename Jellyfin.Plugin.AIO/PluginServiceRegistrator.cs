using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AIO;

/// <summary>
/// Registers JellyTube services into Jellyfin's DI container at startup.
/// The <see cref="JellyTubeStartupFilter"/> is picked up by ASP.NET Core's
/// IStartupFilter pipeline, which wires both middleware classes before any
/// request reaches a controller.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<IStartupFilter, JellyTubeStartupFilter>();
    }
}
