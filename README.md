# WatchSignal

WatchSignal adds one line to a series page in the Jellyfin web client.

- Next episode: `Next episode S02E05 “The Weight” airs Thursday, November 12, 2026 at 9:00 PM on HBO.`
- New season: `Season 9 premieres Thursday, January 7, 2027 at 9:00 PM on ABC.`

The line uses your Sonarr library when the show is in Sonarr. Otherwise it uses TVMaze, which needs no API key. Times are shown in the viewer's timezone.

## Install

In Jellyfin, open **Dashboard → Plugins → Repositories** and add:

`https://raw.githubusercontent.com/Myxelium/WatchSignal/main/manifest.json`

Then open **Catalog**, install **WatchSignal**, and restart Jellyfin. Refresh the web app and open a series. The line sits under the year and runtime.

Jellyfin 10.11 installs the .NET 9 build. Jellyfin 12 installs the .NET 10 build. The catalog lists the one that matches the server.

Optional: **Dashboard → Plugins → WatchSignal**. Leave dates on Automatic, or set the Sonarr address and API key (Sonarr → Settings → General → Security) and use **Test Sonarr**.

The line is part of the web client. Jellyfin's other apps do not load it.

The catalog is empty until the first push to `main` finishes the release workflow.

## Releases

Every push to `main` is tested, then published as a GitHub release. The workflow writes the zip checksums back into `manifest.json`, which is the repository URL above. A commit that only changes `manifest.json` does not start another release. Put `[skip release]` in a commit message to test without publishing.

Pull requests are tested and not published.

## Build locally

.NET 9 and .NET 10 are both required.

```bash
dotnet test -c Release
TZ=UTC node tests/formatRelease.test.js
python3 tools/pack.py
```
