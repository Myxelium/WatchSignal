using System.Globalization;
using Jellyfin.Plugin.WatchSignal.Configuration;
using Jellyfin.Plugin.WatchSignal.Scheduling;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WatchSignal;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "WatchSignal";

    public override Guid Id => Guid.Parse("79801685-27ac-495f-a917-11298fe58dce");

    public override string Description => "Shows when the next episode or season airs on the series page.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                EnableInMainMenu = false
            }
        ];
    }

    internal static TimeSpan CacheDuration(PluginConfiguration configuration)
    {
        var minutes = configuration.CacheMinutes;
        if (minutes < 1)
        {
            minutes = 1;
        }
        else if (minutes > 7 * 24 * 60)
        {
            minutes = 7 * 24 * 60;
        }

        return TimeSpan.FromMinutes(minutes);
    }

    internal static string VersionLabel { get; } =
        typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1";

    internal static string CacheKey(ShowIdentity show, PluginConfiguration configuration)
    {
        return string.Join(
            '|',
            SourceMode.Normalize(configuration.Source),
            SonarrUrl.NormalizeBaseUrl(configuration.SonarrUrl) ?? string.Empty,
            show.TvdbId ?? string.Empty,
            show.ImdbId ?? string.Empty,
            show.TvMazeId ?? string.Empty,
            show.Name,
            show.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }
}
