using System.Globalization;
using System.Text.Json;

namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal sealed record SonarrSeriesHit(int Id, string? Title, string? Network, DateTimeOffset? NextAiring);

internal static class SonarrParser
{
    internal static SonarrSeriesHit? MatchSeries(string json, string? tvdbId, string? imdbId, string name, int? year)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        SonarrSeriesHit? tvdb = null;
        SonarrSeriesHit? imdb = null;
        SonarrSeriesHit? titled = null;
        var wanted = Normalize(name);

        foreach (var row in document.RootElement.EnumerateArray())
        {
            var hit = ReadSeries(row);
            if (hit is null || hit.Id <= 0)
            {
                continue;
            }

            var rowTvdb = ReadString(row, "tvdbId");
            var rowImdb = ReadString(row, "imdbId");
            if (tvdb is null && IdsEqual(rowTvdb, tvdbId))
            {
                tvdb = hit;
            }

            if (imdb is null && IdsEqual(rowImdb, imdbId))
            {
                imdb = hit;
            }

            if (titled is null && string.Equals(Normalize(hit.Title), wanted, StringComparison.Ordinal) && wanted.Length > 0)
            {
                var rowYear = row.TryGetProperty("year", out var yearElement) && yearElement.TryGetInt32(out var parsed) ? parsed : (int?)null;
                if (year is null || rowYear is null || rowYear == year)
                {
                    titled = hit;
                }
            }
        }

        return tvdb ?? imdb ?? titled;
    }

    internal static UpcomingRelease? SelectEpisodes(string json, DateTimeOffset now, string? network, DateTimeOffset? nextAiring)
    {
        using var document = JsonDocument.Parse(json);
        var airings = new List<Airing>();
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                int? season = ReadInt(row, "seasonNumber");
                int? episode = ReadInt(row, "episodeNumber");
                if (season is < 0)
                {
                    continue;
                }

                var title = ReadString(row, "title");
                var airAt = ReadStamp(ReadString(row, "airDateUtc"));
                var airDate = ReadDate(ReadString(row, "airDate"));
                airings.Add(new Airing(season, episode, title, airAt, airDate));
            }
        }

        var selected = ScheduleSelector.Select(airings, now, network, "sonarr");
        if (selected is not null)
        {
            return selected;
        }

        if (nextAiring is DateTimeOffset upcoming && upcoming >= now)
        {
            return new UpcomingRelease
            {
                Found = true,
                Kind = "episode",
                AirAt = upcoming,
                Network = ScheduleSelector.CleanTitle(network),
                Source = "sonarr"
            };
        }

        return null;
    }

    internal static (string? AppName, string? Version, int? SeriesCount) ReadStatus(string statusJson, string? seriesJson)
    {
        string? appName = null;
        string? version = null;
        using (var status = JsonDocument.Parse(statusJson))
        {
            appName = ReadString(status.RootElement, "appName");
            version = ReadString(status.RootElement, "version");
        }

        int? count = null;
        if (!string.IsNullOrWhiteSpace(seriesJson))
        {
            using var series = JsonDocument.Parse(seriesJson);
            if (series.RootElement.ValueKind == JsonValueKind.Array)
            {
                count = series.RootElement.GetArrayLength();
            }
        }

        return (appName, version, count);
    }

    private static SonarrSeriesHit? ReadSeries(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadInt(row, "id") ?? 0;
        return new SonarrSeriesHit(id, ReadString(row, "title"), ReadString(row, "network"), ReadStamp(ReadString(row, "nextAiring")));
    }

    private static bool IdsEqual(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static DateTimeOffset? ReadStamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    private static DateOnly? ReadDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                chars.Add(char.ToLowerInvariant(character));
            }
        }

        return new string(chars.ToArray());
    }
}
