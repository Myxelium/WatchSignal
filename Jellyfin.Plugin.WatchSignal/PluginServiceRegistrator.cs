using Jellyfin.Plugin.WatchSignal.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WatchSignal;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddMemoryCache();
        serviceCollection.AddHttpClient("watchsignal-tvmaze", TvMazeHttp.Configure)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(8)
            });
        serviceCollection.AddHttpClient("watchsignal-sonarr", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    ConnectTimeout = TimeSpan.FromSeconds(8)
                };
                handler.SslOptions.RemoteCertificateValidationCallback = SonarrClient.IgnoreCertificateErrors;
                return handler;
            });

        serviceCollection.AddSingleton<TvMazeClient>();
        serviceCollection.AddSingleton<SonarrClient>();
        serviceCollection.AddSingleton<UpcomingService>();
        serviceCollection.AddSingleton<IStartupFilter, ScriptInjectionStartupFilter>();
    }
}
