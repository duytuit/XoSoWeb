using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;
using XoSoWeb.Shares;
using XoSoWeb.Shares.BaseRepository;
using XoSoWeb.Shares.Connects;
using Microsoft.Extensions.Options;
using XoSoWeb.Middlewares;
using XoSoWeb.Lotterys;

namespace XoSoWeb
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var httpContextAccessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
            services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(httpContextAccessor);
            var site = new SiteSettings(httpContextAccessor);
            Configuration.GetSection("Site").Bind(site);
            site.Validate(Environment.IsDevelopment());
            if (!Environment.IsProduction()) site.AllowIndexing = false;
            if (Environment.IsStaging() && (string.IsNullOrWhiteSpace(site.StagingUsername) || string.IsNullOrWhiteSpace(site.StagingPassword)))
                throw new InvalidOperationException("Staging requires Site:StagingUsername and Site:StagingPassword.");
            services.AddSingleton(site);
            services.AddMemoryCache();
            services.AddSingleton<ArchiveCatalog>();
            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = Uri.TryCreate(site.SiteUrl, UriKind.Absolute, out var origin)
                    && origin.Scheme == Uri.UriSchemeHttps ? origin.Port : 443;
                options.RedirectStatusCode = 308;
            });

             services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                            .AllowAnyOrigin() // hoặc .WithOrigins("https://your-frontend.com")
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
            });
            // kết nối redis
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse(Configuration.GetConnectionString("Redis"), true);
                configuration.ResolveDns = true;
                return ConnectionMultiplexer.Connect(configuration);
            });
            // kết nối sql server
            services.AddDbContext<XoSoDBContext>(options =>
                {
                        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
                        // Sensitive data logging is intentionally disabled.
                        options.EnableDetailedErrors();
                }
            );
            // kết nối sql server kiểu ado
            services.Configure<ConnectionStrings>(Configuration.GetSection("ConnectionStrings"));
            services.AddTransient<AdoXoSoDB>();
            services.AddSingleton<RedisService>();
            
            services.AddResponseCompression(options => options.EnableForHttps = true);
            services.AddControllersWithViews(); // 👈 hỗ trợ cả View + API
            services.AddHttpClient(); // Add HttpClient factory
            services.AddSingleton<TelegramContactService>();
            services.AddLotteryModule();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure( IApplicationBuilder app, IWebHostEnvironment env)
        {
            var telegramConfig = app.ApplicationServices.GetRequiredService<IOptions<TelegramSettings>>();
            Helper.ConfigureTelegram(telegramConfig.Value);

            app.UseMiddleware<ExceptionMiddleware>();
            if (!env.IsDevelopment()) app.UseResponseCompression();
            app.UseMiddleware<PublicUrlMiddleware>();
            if (Uri.TryCreate(app.ApplicationServices.GetRequiredService<SiteSettings>().SiteUrl, UriKind.Absolute, out var origin)
                && origin.Scheme == Uri.UriSchemeHttps)
                app.UseHttpsRedirection();
            app.UseMiddleware<StagingAccessMiddleware>();
            app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context =>
            {
                context.Context.Response.Headers["Cache-Control"] = context.Context.Request.Query.ContainsKey("v")
                    ? "public,max-age=31536000,immutable" : "public,max-age=3600";
            }});

            app.UseRouting();

            app.UseCors("AllowAll"); // Quan trọng: phải đặt trước UseAuthorization nếu có

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers(); // API route
            });
        }
    }
}
