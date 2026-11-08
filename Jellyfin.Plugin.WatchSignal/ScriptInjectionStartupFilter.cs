using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.WatchSignal;

/// <summary>
/// Inserts the series-page script into jellyfin-web's index.html as it is served.
/// The web folder stays untouched, so a read-only container and a plugin uninstall both stay clean.
/// </summary>
public sealed class ScriptInjectionStartupFilter : IStartupFilter
{
    private readonly ILogger<ScriptInjectionStartupFilter> _logger;
    private int _loggedOnce;

    public ScriptInjectionStartupFilter(ILogger<ScriptInjectionStartupFilter> logger)
    {
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InvokeAsync);
            next(app);
        };
    }

    private async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !IsIndexRequest(context.Request.Path.Value))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("Range");
        context.Request.Headers.Remove("If-Range");
        context.Request.Headers.Remove("If-None-Match");
        context.Request.Headers.Remove("If-Modified-Since");

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            context.Response.Body = originalBody;
            throw;
        }

        context.Response.Body = originalBody;
        buffer.Position = 0;

        var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
            && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!isHtml)
        {
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            return;
        }

        string html;
        using (var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            html = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        var marker = "/WatchSignal/client.js";
        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (html.Contains(marker, StringComparison.OrdinalIgnoreCase) || bodyClose < 0)
        {
            var unchanged = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength = unchanged.Length;
            await originalBody.WriteAsync(unchanged).ConfigureAwait(false);
            return;
        }

        var prefix = context.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        if (prefix.Length == 0)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var web = path.LastIndexOf("/web", StringComparison.OrdinalIgnoreCase);
            if (web > 0)
            {
                prefix = path[..web];
            }
        }

        var tag = "<script defer src=\""
            + System.Net.WebUtility.HtmlEncode(prefix)
            + "/WatchSignal/client.js?v="
            + System.Net.WebUtility.HtmlEncode(Plugin.VersionLabel)
            + "\"></script>";
        html = html.Insert(bodyClose, tag + "\n");

        if (Interlocked.Exchange(ref _loggedOnce, 1) == 0)
        {
            _logger.LogInformation("WatchSignal injected its series-page script");
        }

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        context.Response.Headers.Remove("Accept-Ranges");
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        await originalBody.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static bool IsIndexRequest(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase);
    }
}
