using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace XoSoWeb.Chat
{
    public class ChatOriginMiddleware
    {
        private readonly RequestDelegate next;
        public ChatOriginMiddleware(RequestDelegate next) => this.next = next;
        public async Task InvokeAsync(HttpContext context)
        {
            // CORS alone does not protect WebSocket handshakes.
            if (context.Request.Path.StartsWithSegments("/community-chat"))
            {
                var origin = context.Request.Headers["Origin"].ToString();
                var expected = context.Request.Scheme + "://" + context.Request.Host;
                if ((origin.Length > 0 && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase))
                    || context.Request.Headers["Sec-Fetch-Site"] == "cross-site")
                { context.Response.StatusCode = 403; return; }
            }
            await next(context);
        }
    }
}
