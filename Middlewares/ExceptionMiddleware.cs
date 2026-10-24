using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace XoSoWeb.Middlewares
{
    public sealed class ExceptionMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<ExceptionMiddleware> logger;
        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger) { this.next = next; this.logger = logger; }
        public async Task InvokeAsync(HttpContext context)
        {
            try { await next(context); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception exception)
            {
                // Detailed failures stay in server logs; no request bodies, credentials or external notifications.
                logger.LogError(exception, "Request failed: {Method} {Path}; trace {TraceId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
                if (context.Response.HasStarted) { context.Abort(); return; }
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.Headers["X-Robots-Tag"] = "noindex, follow";
                context.Response.Headers["Cache-Control"] = "no-store";
                if (context.Request.Headers["Accept"].ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><meta name=\"robots\" content=\"noindex,follow\"><title>Tạm thời chưa thể tải trang</title></head><body><main><h1>Tạm thời chưa thể tải trang</h1><p>Vui lòng thử lại sau. Mã yêu cầu: " + System.Net.WebUtility.HtmlEncode(context.TraceIdentifier) + "</p><a href=\"/\">Về trang chủ</a></main></body></html>");
                }
                else await context.Response.WriteAsJsonAsync(new { success = false, message = "Tạm thời chưa thể xử lý yêu cầu.", traceId = context.TraceIdentifier });
            }
        }
    }
}
