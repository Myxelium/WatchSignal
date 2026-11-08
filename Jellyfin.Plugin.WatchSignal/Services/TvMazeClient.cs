using System.Net;
using System.Net.Http.Headers;
using Jellyfin.Plugin.WatchSignal.Scheduling;

namespace Jellyfin.Plugin.WatchSignal.Services;

public sealed class TvMazeClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TvMazeClient> _logger;

    public TvMazeClient(IHttpClientFactory httpClientFactory, ILogger<TvMazeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    internal async Task<UpcomingRelease?> LookupAsync(ShowIdentity show, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var showId = Digits(show.TvMazeId);
        if (showId is null)
        {
            var lookedUp = await FindShowIdAsync(show, cancellationToken).ConfigureAwait(false);
            showId = lookedUp?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (showId is null)
        {
            return null;
        }

        var episodeJson = await GetAsync("shows/" + showId + "?embed=nextepisode", cancellationToken).ConfigureAwait(false);
        if (episodeJson is null)
        {
            return null;
        }

        var next = TvMazeParser.ParseNextEpisode(episodeJson, now);
        if (next is not null)
        {
            return next;
        }

        var seasonsJson = await GetAsync("shows/" + showId + "/seasons", cancellationToken).ConfigureAwait(false);
        return seasonsJson is null ? null : TvMazeParser.ParseSeasonPremieres(seasonsJson, now);
    }

    private async Task<int?> FindShowIdAsync(ShowIdentity show, CancellationToken cancellationToken)
    {
        if (Digits(show.TvdbId) is string tvdb)
        {
            var body = await GetAsync("lookup/shows?thetvdb=" + tvdb, cancellationToken).ConfigureAwait(false);
            var id = body is null ? null : TvMazeParser.ReadShowId(body);
            if (id is not null)
            {
                return id;
            }
        }

        if (IsImdb(show.ImdbId))
        {
            var body = await GetAsync("lookup/shows?imdb=" + Uri.EscapeDataString(show.ImdbId!), cancellationToken).ConfigureAwait(false);
            var id = body is null ? null : TvMazeParser.ReadShowId(body);
            if (id is not null)
            {
                return id;
            }
        }

        if (string.IsNullOrWhiteSpace(show.Name))
        {
            return null;
        }

        var search = await GetAsync("search/shows?q=" + Uri.EscapeDataString(show.Name), cancellationToken).ConfigureAwait(false);
        if (search is null)
        {
            return null;
        }

        var picked = TvMazeParser.PickShowId(search, show.Name, show.Year);
        if (picked is null)
        {
            _logger.LogDebug("TVMaze had no exact match for {Name}", show.Name);
        }

        return picked;
    }

    private async Task<string?> GetAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("watchsignal-tvmaze");
        using var response = await client.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.All(char.IsDigit) ? trimmed : null;
    }

    private static bool IsImdb(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 2
            && trimmed.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
            && trimmed[2..].All(char.IsDigit);
    }
}

internal static class TvMazeHttp
{
    internal static void Configure(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.tvmaze.com/");
        client.Timeout = TimeSpan.FromSeconds(12);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WatchSignal/1.0 (Jellyfin plugin)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
