using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using XoSoWeb.Services;

namespace XoSoWeb.Middlewares
{
    public sealed class StagingAccessMiddleware
    {
        private readonly RequestDelegate next;
        public StagingAccessMiddleware(RequestDelegate next) { this.next = next; }
        public async Task InvokeAsync(HttpContext context, SiteSettings site, IWebHostEnvironment environment)
        {
            if (environment.IsStaging())
            {
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                var authorization = context.Request.Headers.Authorization.ToString();
                var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(site.StagingUsername + ":" + site.StagingPassword));
                if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
                    || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authorization.Substring(6)), Encoding.UTF8.GetBytes(expected)))
                {
                    context.Response.StatusCode = 401;
                    context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Staging\", charset=\"UTF-8\"";
                    return;
                }
            }
            await next(context);
        }
    }
}
