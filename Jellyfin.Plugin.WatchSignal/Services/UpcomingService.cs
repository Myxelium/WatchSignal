using Jellyfin.Plugin.WatchSignal.Configuration;
using Jellyfin.Plugin.WatchSignal.Scheduling;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.WatchSignal.Services;

public sealed class UpcomingService
{
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromMinutes(2);

    private readonly TvMazeClient _tvMaze;
    private readonly SonarrClient _sonarr;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UpcomingService> _logger;

    public UpcomingService(TvMazeClient tvMaze, SonarrClient sonarr, IMemoryCache cache, ILogger<UpcomingService> logger)
    {
        _tvMaze = tvMaze;
        _sonarr = sonarr;
        _cache = cache;
        _logger = logger;
    }

    internal async Task<UpcomingRelease> GetAsync(ShowIdentity show, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!configuration.Enabled)
        {
            return UpcomingRelease.None;
        }

        var key = Plugin.CacheKey(show, configuration);
        if (_cache.TryGetValue(key, out UpcomingRelease? cached) && cached is not null)
        {
            return cached;
        }

        var (release, ttl) = await ResolveAsync(show, configuration, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, release, ttl);
        return release;
    }

    private async Task<(UpcomingRelease Release, TimeSpan Ttl)> ResolveAsync(
        ShowIdentity show,
        PluginConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var source = SourceMode.Normalize(configuration.Source);
        var ttl = Plugin.CacheDuration(configuration);
        var now = DateTimeOffset.UtcNow;
        var sonarrFailed = false;

        if (source is SourceMode.Auto or SourceMode.Sonarr)
        {
            try
            {
                var sonarr = await _sonarr.LookupAsync(show, configuration, now, cancellationToken).ConfigureAwait(false);
                if (sonarr.InLibrary)
                {
                    return (sonarr.Release ?? UpcomingRelease.None, ttl);
                }

                if (source == SourceMode.Sonarr)
                {
                    return (UpcomingRelease.None, ttl);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                sonarrFailed = true;
                _logger.LogInformation(ex, "Sonarr lookup failed for {Name}", show.Name);
                if (source == SourceMode.Sonarr)
                {
                    return (UpcomingRelease.None, ErrorTtl);
                }
            }
        }

        if (source == SourceMode.Sonarr)
        {
            return (UpcomingRelease.None, ttl);
        }

        try
        {
            var tvMaze = await _tvMaze.LookupAsync(show, now, cancellationToken).ConfigureAwait(false);
            return (tvMaze ?? UpcomingRelease.None, ttl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogInformation(ex, "TVMaze lookup failed for {Name}", show.Name);
            return (UpcomingRelease.None, sonarrFailed ? ErrorTtl : ErrorTtl);
        }
    }
}
