using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchSignal.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = true;

    /// <summary>Auto, Sonarr, or TvMaze.</summary>
    public string Source { get; set; } = SourceMode.Auto;

    public string SonarrUrl { get; set; } = string.Empty;

    public string SonarrApiKey { get; set; } = string.Empty;

    public bool SonarrIgnoreCertificateErrors { get; set; }

    public int CacheMinutes { get; set; } = 360;

    /// <summary>Empty uses the built-in sentence. Otherwise tokens such as {release date}.</summary>
    public string LineFormat { get; set; } = string.Empty;
}
