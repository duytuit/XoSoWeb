using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XoSoWeb.Services;

namespace XoSoWeb.Middlewares
{
    public sealed class PublicUrlMiddleware
    {
        private readonly RequestDelegate next;
        public PublicUrlMiddleware(RequestDelegate next) { this.next = next; }
        private static readonly Dictionary<string, string> aliases = new Dictionary<string, string> {
            ["/home"] = "/", ["/home/index"] = "/", ["/home/xosomienbac"] = "/xo-so-mien-bac",
            ["/home/xosomientrung"] = "/xo-so-mien-trung", ["/home/xosomiennam"] = "/xo-so-mien-nam",
            ["/home/vietlott"] = "/vietlott", ["/home/statistics"] = "/thong-ke", ["/home/lotofrequency"] = "/thong-ke-tan-suat-loto",
            ["/home/about"] = "/lien-he-quang-cao", ["/home/privacy"] = "/privacy", ["/home/terms"] = "/terms", ["/home/support"] = "/support" };
        public async Task InvokeAsync(HttpContext context, SiteSettings site)
        {
            if (!site.AllowIndexing) context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            var path = context.Request.Path.Value ?? "/";
            if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) && !path.Contains('.')
                && !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && !context.WebSockets.IsWebSocketRequest)
            {
                var normalized = path.ToLowerInvariant().TrimEnd('/');
                var drawMarker = path.IndexOf("/ky/", StringComparison.OrdinalIgnoreCase);
                if (drawMarker >= 0) normalized = path.Substring(0, drawMarker).ToLowerInvariant() + "/ky/" + path.Substring(drawMarker + 4).TrimEnd('/');
                if (normalized == "") normalized = "/";
                if (aliases.TryGetValue(normalized, out var target)) normalized = target;
                if (normalized != path)
                {
                    context.Response.StatusCode = 301;
                    // Absolute configured origin prevents a //path redirect from becoming a foreign host.
                    context.Response.Headers.Location = site.Absolute(normalized) + context.Request.QueryString;
                    return;
                }
            }
            await next(context);
        }
    }
}
