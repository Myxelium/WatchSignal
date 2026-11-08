namespace Jellyfin.Plugin.WatchSignal.Scheduling;

/// <summary>
/// The next thing a series will release, or <see cref="None"/> when nothing is scheduled.
/// </summary>
public sealed record UpcomingRelease
{
    public static UpcomingRelease None { get; } = new() { Found = false };

    public bool Found { get; init; }

    /// <summary>episode, season, or special.</summary>
    public string? Kind { get; init; }

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    public string? EpisodeTitle { get; init; }

    public DateTimeOffset? AirAt { get; init; }

    /// <summary>Calendar date (yyyy-MM-dd) when the feed has no time of day.</summary>
    public string? AirDate { get; init; }

    public string? Network { get; init; }

    public string? Source { get; init; }
}
