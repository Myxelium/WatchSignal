using System.Security.Claims;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.WatchSignal.Scheduling;
using Jellyfin.Plugin.WatchSignal.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.WatchSignal;

[ApiController]
[Authorize]
[Route("WatchSignal")]
public sealed class WatchSignalController : ControllerBase
{
    private const string ScriptResource = "Jellyfin.Plugin.WatchSignal.Web.watchsignal.js";

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly UpcomingService _upcoming;
    private readonly SonarrClient _sonarr;

    public WatchSignalController(
        ILibraryManager libraryManager,
        IUserManager userManager,
        UpcomingService upcoming,
        SonarrClient sonarr)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _upcoming = upcoming;
        _sonarr = sonarr;
    }

    [HttpGet("client.js")]
    [AllowAnonymous]
    public IActionResult ClientScript()
    {
        var stream = typeof(WatchSignalController).Assembly.GetManifestResourceStream(ScriptResource);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(stream, "application/javascript; charset=utf-8");
    }

    [HttpGet("Series/{itemId}/Upcoming")]
    public async Task<ActionResult<UpcomingRelease>> Upcoming(Guid itemId, CancellationToken cancellationToken)
    {
        var user = CurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        var series = _libraryManager.GetItemById<Series>(itemId, user);
        if (series is null)
        {
            return NotFound();
        }

        var show = new ShowIdentity(
            series.Name,
            series.ProductionYear,
            ProviderId(series, "Tvdb"),
            ProviderId(series, "Imdb"),
            ProviderId(series, "TvMaze", "TVmaze", "TVMaze"));
        var release = await _upcoming.GetAsync(show, cancellationToken).ConfigureAwait(false);
        var format = Plugin.Instance?.Configuration.LineFormat;
        if (!string.IsNullOrWhiteSpace(format))
        {
            release = release with { LineFormat = format.Trim() };
        }

        return Ok(release);
    }

    [HttpPost("Sonarr/Test")]
    public async Task<ActionResult<SonarrTestResponse>> TestSonarr([FromBody] SonarrTestRequest request, CancellationToken cancellationToken)
    {
        var user = CurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!IsAdministrator(user))
        {
            return Forbid();
        }

        var result = await _sonarr.ProbeAsync(
            request.Url,
            request.ApiKey,
            request.IgnoreCertificateErrors,
            cancellationToken).ConfigureAwait(false);
        return Ok(new SonarrTestResponse { Ok = result.Ok, Message = result.Message });
    }

    private User? CurrentUser()
    {
        var raw = User.FindFirstValue("Jellyfin-UserId")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var userId))
        {
            return null;
        }

        return _userManager.GetUserById(userId);
    }

    private static bool IsAdministrator(User user)
    {
        foreach (var permission in user.Permissions)
        {
            if (permission.Kind == PermissionKind.IsAdministrator && permission.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ProviderId(Series series, params string[] names)
    {
        foreach (var pair in series.ProviderIds)
        {
            foreach (var name in names)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    return pair.Value.Trim();
                }
            }
        }

        return null;
    }
}

public sealed class SonarrTestRequest
{
    public string? Url { get; set; }

    public string? ApiKey { get; set; }

    public bool IgnoreCertificateErrors { get; set; }
}

public sealed class SonarrTestResponse
{
    public bool Ok { get; set; }

    public string Message { get; set; } = string.Empty;
}
