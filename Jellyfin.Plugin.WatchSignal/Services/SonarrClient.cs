using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Jellyfin.Plugin.WatchSignal.Configuration;
using Jellyfin.Plugin.WatchSignal.Scheduling;

namespace Jellyfin.Plugin.WatchSignal.Services;

internal readonly record struct SonarrLookup(bool Configured, bool InLibrary, UpcomingRelease? Release);

internal readonly record struct SonarrProbe(bool Ok, string Message);

public sealed class SonarrClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SonarrClient> _logger;

    public SonarrClient(IHttpClientFactory httpClientFactory, ILogger<SonarrClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    internal async Task<SonarrLookup> LookupAsync(
        ShowIdentity show,
        PluginConfiguration configuration,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var baseUrl = SonarrUrl.NormalizeBaseUrl(configuration.SonarrUrl);
        var apiKey = configuration.SonarrApiKey?.Trim();
        if (baseUrl is null || string.IsNullOrEmpty(apiKey))
        {
            return new SonarrLookup(false, false, null);
        }

        var client = _httpClientFactory.CreateClient("watchsignal-sonarr");
        var seriesJson = await GetSeriesJsonAsync(client, baseUrl, apiKey, show, cancellationToken).ConfigureAwait(false);
        var hit = seriesJson is null
            ? null
            : SonarrParser.MatchSeries(seriesJson, show.TvdbId, show.ImdbId, show.Name, show.Year);
        if (hit is null)
        {
            return new SonarrLookup(true, false, null);
        }

        var episodes = await SendAsync(client, baseUrl, apiKey, "episode?seriesId=" + hit.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);
        var release = episodes is null
            ? null
            : SonarrParser.SelectEpisodes(episodes, now, hit.Network, hit.NextAiring);
        return new SonarrLookup(true, true, release);
    }

    internal async Task<SonarrProbe> ProbeAsync(string? url, string? apiKey, bool ignoreCertificateErrors, CancellationToken cancellationToken)
    {
        var baseUrl = SonarrUrl.NormalizeBaseUrl(url);
        var key = apiKey?.Trim();
        if (baseUrl is null)
        {
            return new SonarrProbe(false, "Enter an http or https address for Sonarr.");
        }

        if (string.IsNullOrEmpty(key))
        {
            return new SonarrProbe(false, "Enter the Sonarr API key.");
        }

        using var client = CreateProbeClient(ignoreCertificateErrors);
        try
        {
            var status = await SendAsync(client, baseUrl, key, "system/status", cancellationToken).ConfigureAwait(false);
            if (status is null)
            {
                return new SonarrProbe(false, "Sonarr did not answer.");
            }

            string? series = null;
            try
            {
                series = await SendAsync(client, baseUrl, key, "series", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogDebug(ex, "Sonarr status succeeded but the series list did not");
            }

            var parsed = SonarrParser.ReadStatus(status, series);
            if (!string.IsNullOrWhiteSpace(parsed.AppName)
                && !parsed.AppName.Contains("Sonarr", StringComparison.OrdinalIgnoreCase))
            {
                return new SonarrProbe(false, "That address answered as " + parsed.AppName + ", not Sonarr.");
            }

            var version = string.IsNullOrWhiteSpace(parsed.Version) ? "Sonarr" : "Sonarr " + parsed.Version;
            var library = parsed.SeriesCount is int count ? " " + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " series in the library." : string.Empty;
            return new SonarrProbe(true, "Connected to " + version + "." + library);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new SonarrProbe(false, "Sonarr rejected the API key.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogInformation(ex, "Sonarr probe failed for host {Host}", new Uri(baseUrl).Host);
            return new SonarrProbe(false, "Could not reach Sonarr.");
        }
    }

    private async Task<string?> GetSeriesJsonAsync(
        HttpClient client,
        string baseUrl,
        string apiKey,
        ShowIdentity show,
        CancellationToken cancellationToken)
    {
        if (IsSafeToken(show.TvdbId))
        {
            return await SendAsync(client, baseUrl, apiKey, "series/lookup?term=tvdb:" + Uri.EscapeDataString(show.TvdbId!.Trim()), cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsSafeToken(show.ImdbId))
        {
            return await SendAsync(client, baseUrl, apiKey, "series/lookup?term=imdb:" + Uri.EscapeDataString(show.ImdbId!.Trim()), cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(show.Name))
        {
            return null;
        }

        return await SendAsync(client, baseUrl, apiKey, "series/lookup?term=" + Uri.EscapeDataString(show.Name.Trim()), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string?> SendAsync(
        HttpClient client,
        string baseUrl,
        string apiKey,
        string relative,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/api/v3/" + relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpClient CreateProbeClient(bool ignoreCertificateErrors)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(8)
        };
        if (ignoreCertificateErrors)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(20) };
    }

    internal static bool IgnoreCertificateErrors(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        return errors == SslPolicyErrors.None || (Plugin.Instance?.Configuration.SonarrIgnoreCertificateErrors ?? false);
    }

    private static bool IsSafeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value.Trim())
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
