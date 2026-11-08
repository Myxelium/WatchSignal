namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal sealed record ShowIdentity(
    string Name,
    int? Year,
    string? TvdbId,
    string? ImdbId,
    string? TvMazeId);
