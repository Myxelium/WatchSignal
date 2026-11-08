namespace Jellyfin.Plugin.WatchSignal.Scheduling;

internal static class SonarrUrl
{
    internal static string? NormalizeBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/api/v3".Length];
        }
        else if (path.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/api".Length];
        }

        path = path.TrimEnd('/');
        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port, string.IsNullOrEmpty(path) ? "/" : path);
        return builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
