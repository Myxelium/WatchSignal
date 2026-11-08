using Jellyfin.Plugin.WatchSignal.Scheduling;
using Xunit;

namespace Jellyfin.Plugin.WatchSignal.Tests;

public class ScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Next_episode_one_is_a_season_premiere()
    {
        var json = """
            {
              "name": "The Rookie",
              "network": { "name": "ABC" },
              "_embedded": {
                "nextepisode": {
                  "name": "TBA",
                  "season": 9,
                  "number": 1,
                  "airdate": "2027-01-06",
                  "airstamp": "2027-01-07T02:00:00+00:00"
                }
              }
            }
            """;

        var release = TvMazeParser.ParseNextEpisode(json, Now);

        Assert.NotNull(release);
        Assert.Equal("season", release.Kind);
        Assert.Equal(9, release.SeasonNumber);
        Assert.Equal(1, release.EpisodeNumber);
        Assert.Null(release.EpisodeTitle);
        Assert.Equal("ABC", release.Network);
        Assert.Equal("tvmaze", release.Source);
        Assert.Equal(new DateTimeOffset(2027, 1, 7, 2, 0, 0, TimeSpan.Zero), release.AirAt);
        Assert.Equal("2027-01-06", release.AirDate);
    }

    [Fact]
    public void Ended_show_without_a_next_episode_is_empty()
    {
        const string json = """{ "name": "Breaking Bad", "status": "Ended", "network": { "name": "AMC" } }""";

        Assert.Null(TvMazeParser.ParseNextEpisode(json, Now));
    }

    [Fact]
    public void Future_season_premiere_date_is_used_when_no_episode_is_listed()
    {
        const string json = """
            [
              { "number": 1, "premiereDate": "2008-01-20", "endDate": "2008-03-01", "network": { "name": "AMC" } },
              { "number": 2, "premiereDate": "2027-06-01", "network": { "name": "HBO" } }
            ]
            """;

        var release = TvMazeParser.ParseSeasonPremieres(json, Now);

        Assert.NotNull(release);
        Assert.Equal("season", release.Kind);
        Assert.Equal(2, release.SeasonNumber);
        Assert.Equal("HBO", release.Network);
        Assert.Equal("2027-06-01", release.AirDate);
        Assert.Null(release.AirAt);
    }

    [Fact]
    public void Sonarr_skips_aired_episodes_and_marks_episode_one_as_a_new_season()
    {
        const string json = """
            [
              { "seasonNumber": 1, "episodeNumber": 8, "title": "End", "airDateUtc": "2024-05-01T01:00:00Z", "airDate": "2024-04-30" },
              { "seasonNumber": 2, "episodeNumber": 1, "title": "Return", "airDateUtc": "2027-02-10T02:00:00Z", "airDate": "2027-02-09" },
              { "seasonNumber": 2, "episodeNumber": 2, "title": "Next", "airDateUtc": "2027-02-17T02:00:00Z", "airDate": "2027-02-16" }
            ]
            """;

        var release = SonarrParser.SelectEpisodes(json, Now, "ABC", null);

        Assert.NotNull(release);
        Assert.Equal("season", release.Kind);
        Assert.Equal(2, release.SeasonNumber);
        Assert.Equal(1, release.EpisodeNumber);
        Assert.Equal("Return", release.EpisodeTitle);
        Assert.Equal("sonarr", release.Source);
    }

    [Fact]
    public void Mid_season_episode_stays_an_episode()
    {
        const string json = """
            [
              { "seasonNumber": 2, "episodeNumber": 5, "title": "The Weight", "airDateUtc": "2026-11-12T02:00:00Z" }
            ]
            """;

        var release = SonarrParser.SelectEpisodes(json, Now, "HBO", null);

        Assert.NotNull(release);
        Assert.Equal("episode", release.Kind);
        Assert.Equal(2, release.SeasonNumber);
        Assert.Equal(5, release.EpisodeNumber);
        Assert.Equal("The Weight", release.EpisodeTitle);
    }

    [Fact]
    public void A_sooner_special_is_the_next_release()
    {
        const string json = """
            [
              { "seasonNumber": 0, "episodeNumber": 1, "title": "Holiday", "airDateUtc": "2026-12-25T02:00:00Z" },
              { "seasonNumber": 3, "episodeNumber": 1, "title": "Premiere", "airDateUtc": "2027-01-06T02:00:00Z" }
            ]
            """;

        var release = SonarrParser.SelectEpisodes(json, Now, null, null);

        Assert.NotNull(release);
        Assert.Equal("special", release.Kind);
        Assert.Equal("Holiday", release.EpisodeTitle);
    }

    [Fact]
    public void Undated_episodes_fall_back_to_the_series_next_airing()
    {
        const string json = """[{ "seasonNumber": 3, "episodeNumber": 1, "title": "Soon", "airDateUtc": null }]""";
        var next = new DateTimeOffset(2027, 3, 1, 2, 0, 0, TimeSpan.Zero);

        var release = SonarrParser.SelectEpisodes(json, Now, "FX", next);

        Assert.NotNull(release);
        Assert.Equal("episode", release.Kind);
        Assert.Equal(next, release.AirAt);
        Assert.Null(release.SeasonNumber);
    }

    [Fact]
    public void Lookup_prefers_the_copy_that_is_already_in_sonarr()
    {
        const string json = """
            [
              { "id": 0, "title": "The Rookie", "tvdbId": 350665, "year": 2018 },
              { "id": 12, "title": "The Rookie", "tvdbId": 350665, "year": 2018, "network": "ABC" }
            ]
            """;

        var hit = SonarrParser.MatchSeries(json, "350665", null, "The Rookie", 2018);

        Assert.NotNull(hit);
        Assert.Equal(12, hit.Id);
        Assert.Equal("ABC", hit.Network);
    }

    [Fact]
    public void Search_requires_the_same_name_and_year()
    {
        const string json = """
            [
              { "score": 0.7, "show": { "id": 1, "name": "Rookie", "premiered": "2018-10-16" } },
              { "score": 0.99, "show": { "id": 2, "name": "The Rookie", "premiered": "2018-10-16" } },
              { "score": 0.5, "show": { "id": 3, "name": "The Rookie", "premiered": "2000-01-01" } }
            ]
            """;

        Assert.Equal(2, TvMazeParser.PickShowId(json, "The Rookie (2018)", 2018));
        Assert.Null(TvMazeParser.PickShowId(json, "Something Else", 2018));
    }

    [Theory]
    [InlineData("http://192.168.1.20:8989", "http://192.168.1.20:8989")]
    [InlineData("http://192.168.1.20:8989/", "http://192.168.1.20:8989")]
    [InlineData("http://192.168.1.20:8989/api/v3", "http://192.168.1.20:8989")]
    [InlineData("https://sonarr.example/sonarr/api", "https://sonarr.example/sonarr")]
    [InlineData("ftp://192.168.1.20", null)]
    [InlineData("http://user:secret@host:8989", null)]
    [InlineData("not a url", null)]
    public void Sonarr_address_is_reduced_to_the_server_root(string raw, string? expected)
    {
        Assert.Equal(expected, SonarrUrl.NormalizeBaseUrl(raw));
    }

    [Fact]
    public void Client_script_and_config_page_are_embedded()
    {
        var names = typeof(TvMazeParser).Assembly.GetManifestResourceNames();
        Assert.Contains("Jellyfin.Plugin.WatchSignal.Web.watchsignal.js", names);
        Assert.Contains("Jellyfin.Plugin.WatchSignal.Configuration.configPage.html", names);
    }
}
