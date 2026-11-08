namespace Jellyfin.Plugin.WatchSignal.Configuration;

public static class SourceMode
{
    public const string Auto = "Auto";

    public const string Sonarr = "Sonarr";

    public const string TvMaze = "TvMaze";

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Sonarr, StringComparison.OrdinalIgnoreCase))
        {
            return Sonarr;
        }

        if (string.Equals(value, TvMaze, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TVMaze", StringComparison.OrdinalIgnoreCase))
        {
            return TvMaze;
        }

        return Auto;
    }
}
