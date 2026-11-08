using System.Globalization;
using System.Text.Json;

namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal static class TvMazeParser
{
    internal static int? ReadShowId(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return id.GetInt32();
    }

    internal static int? PickShowId(string searchJson, string name, int? year)
    {
        using var document = JsonDocument.Parse(searchJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var wanted = Normalize(StripYearSuffix(name, year));
        if (wanted.Length == 0)
        {
            return null;
        }

        int? bestId = null;
        double bestScore = double.NegativeInfinity;
        foreach (var row in document.RootElement.EnumerateArray())
        {
            if (!row.TryGetProperty("show", out var show) || show.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var showName = show.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (!string.Equals(Normalize(showName), wanted, StringComparison.Ordinal))
            {
                continue;
            }

            if (year is int showYear && TryPremieredYear(show, out var premiered) && premiered != showYear)
            {
                continue;
            }

            if (!show.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var score = row.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var parsed)
                ? parsed
                : 0;
            if (score > bestScore)
            {
                bestScore = score;
                bestId = idElement.GetInt32();
            }
        }

        return bestId;
    }

    internal static UpcomingRelease? ParseNextEpisode(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("_embedded", out var embedded)
            || !embedded.TryGetProperty("nextepisode", out var episode)
            || episode.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var airing = ReadEpisode(episode);
        return ScheduleSelector.Select([airing], now, ReadNetwork(root), "tvmaze");
    }

    internal static UpcomingRelease? ParseSeasonPremieres(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var airings = new List<(Airing Airing, string? Network)>();
        foreach (var season in document.RootElement.EnumerateArray())
        {
            if (season.ValueKind != JsonValueKind.Object
                || !season.TryGetProperty("premiereDate", out var premiere)
                || premiere.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var date = ReadDate(premiere.GetString());
            if (date is null)
            {
                continue;
            }

            int? number = season.TryGetProperty("number", out var numberElement) && numberElement.TryGetInt32(out var parsed)
                ? parsed
                : null;
            if (number is null or <= 0)
            {
                continue;
            }

            airings.Add((new Airing(number, 1, null, null, date), ReadNetwork(season)));
        }

        var selected = ScheduleSelector.Select(airings.Select(row => row.Airing), now, null, "tvmaze");
        if (selected is null)
        {
            return null;
        }

        var network = airings.FirstOrDefault(row => row.Airing.SeasonNumber == selected.SeasonNumber).Network;
        return selected with { Network = ScheduleSelector.CleanTitle(network) };
    }

    private static Airing ReadEpisode(JsonElement episode)
    {
        int? season = episode.TryGetProperty("season", out var seasonElement) && seasonElement.TryGetInt32(out var seasonNumber)
            ? seasonNumber
            : null;
        int? number = episode.TryGetProperty("number", out var numberElement) && numberElement.TryGetInt32(out var episodeNumber)
            ? episodeNumber
            : null;
        var title = episode.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var airAt = episode.TryGetProperty("airstamp", out var stamp) ? ReadStamp(stamp.GetString()) : null;
        var airDate = episode.TryGetProperty("airdate", out var date) ? ReadDate(date.GetString()) : null;
        return new Airing(season, number, title, airAt, airDate);
    }

    private static string? ReadNetwork(JsonElement element)
    {
        foreach (var property in new[] { "network", "webChannel" })
        {
            if (element.TryGetProperty(property, out var channel)
                && channel.ValueKind == JsonValueKind.Object
                && channel.TryGetProperty("name", out var name)
                && name.ValueKind == JsonValueKind.String)
            {
                var value = name.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryPremieredYear(JsonElement show, out int year)
    {
        year = 0;
        if (!show.TryGetProperty("premiered", out var premiered) || premiered.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = premiered.GetString();
        return text is { Length: >= 4 } && int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year);
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

    private static string StripYearSuffix(string name, int? year)
    {
        if (year is null)
        {
            return name;
        }

        var suffix = "(" + year.Value.ToString(CultureInfo.InvariantCulture) + ")";
        var trimmed = name.Trim();
        return trimmed.EndsWith(suffix, StringComparison.Ordinal)
            ? trimmed[..^suffix.Length].Trim()
            : trimmed;
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
