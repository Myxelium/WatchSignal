const assert = require("assert");
const path = require("path");

require(path.join(__dirname, "..", "Jellyfin.Plugin.WatchSignal", "Web", "watchsignal.js"));

const { formatRelease } = globalThis.WatchSignalFormat;
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

assert.strictEqual(
    season,
    "Season 9 premieres Thursday, January 7, 2027 at 2:00 AM on ABC."
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

assert.strictEqual(
    episode,
    "Next episode S02E05 \u201cThe Weight\u201d airs Thursday, November 12, 2026 at 2:00 AM on HBO."
);

const today = formatRelease({
    found: true,
    kind: "episode",
    seasonNumber: 1,
    episodeNumber: 4,
    episodeTitle: "Tonight",
    airAt: "2026-11-08T23:00:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(today, "Next episode S01E04 \u201cTonight\u201d airs today at 11:00 PM.");

const tomorrow = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 3,
    airAt: "2026-11-09T18:30:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(tomorrow, "Season 3 premieres tomorrow at 6:30 PM.");

const dateOnly = formatRelease({
    found: true,
    kind: "season",
    seasonNumber: 3,
    airDate: "2027-03-03"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(dateOnly, "Season 3 premieres Wednesday, March 3, 2027.");

const special = formatRelease({
    found: true,
    kind: "special",
    seasonNumber: 0,
    episodeNumber: 1,
    episodeTitle: "Holiday",
    airAt: "2026-12-25T02:00:00Z"
}, at("2026-11-08T12:00:00Z"), locale);

assert.strictEqual(special, "Next special \u201cHoliday\u201d airs Friday, December 25, 2026 at 2:00 AM.");

assert.strictEqual(formatRelease({ found: false }, at("2026-11-08T12:00:00Z"), locale), null);

console.log("formatRelease ok");
