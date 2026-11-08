const assert = require("assert");
const path = require("path");

require(path.join(__dirname, "..", "Jellyfin.Plugin.WatchSignal", "Web", "watchsignal.js"));

const { formatRelease, formatHover } = globalThis.WatchSignalFormat;
const locale = "en-US";

function at(iso) {
    return new Date(iso);
}

const season = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 9,
    episodeNumber: 1,
    episodeTitle: null,
    airAt: "2027-01-07T02:00:00Z",
    network: "ABC"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(season, "59 days and 14 hours to next season");
assert.strictEqual(
    formatHover({
        found: true,
        airAt: "2027-01-07T02:00:00Z"
    }, locale),
    "Thursday, January 7, 2027 at 2:00 AM"
);

const episode = formatRelease({
    found: true,
    kind: "episode",
    seasonNumber: 2,
    episodeNumber: 5,
    episodeTitle: "The Weight",
    airAt: "2026-11-12T02:00:00Z",
    network: "HBO"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(episode, "3 days and 14 hours to next episode");
assert.strictEqual(
    formatHover({
        found: true,
        airAt: "2026-11-12T02:00:00Z"
    }, locale),
    "Thursday, November 12, 2026 at 2:00 AM"
);

const today = formatRelease({
    found: true,
    kind: "episode",
    seasonNumber: 1,
    episodeNumber: 4,
    episodeTitle: "Tonight",
    airAt: "2026-11-08T23:00:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(today, "11 hours to next episode");

const tomorrow = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 3,
    airAt: "2026-11-09T18:30:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(tomorrow, "1 day and 6 hours to next season");

const dateOnly = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 3,
    airDate: "2027-03-03"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(dateOnly, "115 days to next season");
assert.strictEqual(
    formatHover({ found: true, airDate: "2027-03-03" }, locale),
    "Wednesday, March 3, 2027"
);

const special = formatRelease({
    found: true,
    kind: "special",
    seasonNumber: 0,
    episodeNumber: 1,
    episodeTitle: "Holiday",
    airAt: "2026-12-25T02:00:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(special, "46 days and 14 hours to next special");

assert.strictEqual(formatRelease({ found: false }, at("2026-11-08T12:00:00Z"), locale), null);

const custom = formatRelease({
    found: true,
    kind: "episode",
    seasonNumber: 2,
    episodeNumber: 5,
    episodeTitle: "The Weight",
    airAt: "2026-11-12T02:00:00Z",
    network: "HBO"
}, at("2026-11-08T12:00:00Z"), locale, "Next episode {code} airs { release date } on {network}.");

assert.strictEqual(
    custom,
    "Next episode S02E05 airs Thursday, November 12, 2026 at 2:00 AM on HBO."
);

const seasonOnly = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 9,
    episodeNumber: 1,
    airAt: "2027-01-07T02:00:00Z",
    network: "ABC"
}, at("2026-11-08T12:00:00Z"), locale, "{?kind=season}Season {season} premieres {release date}{/kind}{?kind=episode}Next episode {code} airs {release date}{/kind}{?network} on {network}{/network}.");

assert.strictEqual(seasonOnly, "Season 9 premieres Thursday, January 7, 2027 at 2:00 AM on ABC.");

const noTitle = formatRelease({
    found: true,
    kind: "episode",
    seasonNumber: 1,
    episodeNumber: 4,
    airAt: "2026-11-08T23:00:00Z"
}, at("2026-11-08T12:00:00Z"), locale, "Next episode {code}{?title} “{title}”{/title} airs {release date}.");

assert.strictEqual(noTitle, "Next episode S01E04 airs today at 11:00 PM.");

console.log("formatRelease ok");
