namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal sealed record Airing(
    int? SeasonNumber,
    int? EpisodeNumber,
    string? Title,
    DateTimeOffset? AirAt,
    DateOnly? AirDate);
