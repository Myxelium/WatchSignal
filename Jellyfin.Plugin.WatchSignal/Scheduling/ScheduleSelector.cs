using System.Globalization;

namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal static class ScheduleSelector
{
    internal static string Classify(int? season, int? episode)
    {
        if (season == 0)
        {
            return "special";
        }

        if (episode == 1 && season is > 0)
        {
            return "season";
        }

        return "episode";
    }

    internal static UpcomingRelease? Select(
        IEnumerable<Airing> airings,
        DateTimeOffset now,
        string? network,
        string source)
    {
        Airing? best = null;
        DateTimeOffset? bestWhen = null;

        foreach (var airing in airings)
        {
            var when = UpcomingAt(airing, now);
            if (when is null)
            {
                continue;
            }

            if (bestWhen is null || when.Value < bestWhen.Value || (when.Value == bestWhen.Value && ComesFirst(airing, best!)))
            {
                best = airing;
                bestWhen = when;
            }
        }

        if (best is null)
        {
            return null;
        }

        return new UpcomingRelease
        {
            Found = true,
            Kind = Classify(best.SeasonNumber, best.EpisodeNumber),
            SeasonNumber = best.SeasonNumber,
            EpisodeNumber = best.EpisodeNumber,
            EpisodeTitle = CleanTitle(best.Title),
            AirAt = best.AirAt is { } at && at >= now ? at : null,
            AirDate = best.AirDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Network = CleanTitle(network),
            Source = source
        };
    }

    internal static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        if (trimmed.Equals("TBA", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("TBD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static DateTimeOffset? UpcomingAt(Airing airing, DateTimeOffset now)
    {
        if (airing.AirAt is DateTimeOffset at)
        {
            return at >= now ? at : null;
        }

        if (airing.AirDate is DateOnly date && date >= DateOnly.FromDateTime(now.UtcDateTime))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return null;
    }

    private static bool ComesFirst(Airing candidate, Airing current)
    {
        var season = candidate.SeasonNumber ?? int.MaxValue;
        var otherSeason = current.SeasonNumber ?? int.MaxValue;
        if (season != otherSeason)
        {
            return season < otherSeason;
        }

        return (candidate.EpisodeNumber ?? int.MaxValue) < (current.EpisodeNumber ?? int.MaxValue);
    }
}
