using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Syssloappen.Api.Services;

/// <summary>
/// Cache-Control for the built Angular app served from wwwroot. Angular's content-hashed bundles
/// never change under the same name, so browsers may keep them for a year; the entry files that
/// point at them (index.html, the service worker, translations) must be revalidated on every load
/// or a deploy would not reach users. Everything else keeps the default (no header).
/// </summary>
public static partial class StaticFileCaching
{
    private const string Immutable = "public, max-age=31536000, immutable";
    private const string OneWeek = "public, max-age=604800";
    private const string Revalidate = "no-cache";

    private static readonly string[] WeekLongFolders = ["/logo/", "/icons/", "/marketing/", "/fonts/"];

    // Matches Angular's output names, e.g. main-N7VN2EGQ.js, chunk--ToVUsKH.js, styles-RDPY56DM.css.
    [GeneratedRegex(@"^/(main|polyfills|chunk|styles)-[A-Za-z0-9_-]{8,}\.(js|css)$")]
    private static partial Regex HashedBundle();

    public static void Apply(StaticFileResponseContext context)
    {
        var cacheControl = For(context.Context.Request.Path.Value ?? string.Empty, context.File.Name);
        if (cacheControl is not null)
        {
            context.Context.Response.Headers[HeaderNames.CacheControl] = cacheControl;
        }
    }

    /// <param name="requestPath">The request path, e.g. <c>/vuxen</c> for an SPA-fallback request.</param>
    /// <param name="fileName">The file actually served, e.g. <c>index.html</c> for that same request.</param>
    public static string? For(string requestPath, string fileName)
    {
        if (fileName is "index.html" or "ngsw.json" or "ngsw-worker.js" or "safety-worker.js"
            or "worker-basic.min.js" or "manifest.webmanifest"
            || requestPath.StartsWith("/i18n/", StringComparison.Ordinal))
        {
            return Revalidate;
        }

        if (HashedBundle().IsMatch(requestPath))
        {
            return Immutable;
        }

        return WeekLongFolders.Any(folder => requestPath.StartsWith(folder, StringComparison.Ordinal))
            ? OneWeek
            : null;
    }
}
