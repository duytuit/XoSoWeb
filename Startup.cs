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
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
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
                        options.EnableSensitiveDataLogging();
                        options.EnableDetailedErrors();
                }
            );
            // kết nối sql server kiểu ado
            services.Configure<ConnectionStrings>(Configuration.GetSection("ConnectionStrings"));
            services.AddTransient<AdoXoSoDB>();
            services.AddSingleton<RedisService>();
            
            services.AddResponseCompression();
            services.AddControllersWithViews(); // 👈 hỗ trợ cả View + API
            services.AddHttpClient(); // Add HttpClient factory

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure( IApplicationBuilder app, IWebHostEnvironment env)
        {
            var telegramConfig = app.ApplicationServices.GetRequiredService<IOptions<TelegramSettings>>();
            Helper.ConfigureTelegram(telegramConfig.Value);

            app.UseStaticFiles();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("AllowAll"); // Quan trọng: phải đặt trước UseAuthorization nếu có

            app.UseMiddleware<ExceptionMiddleware>();

            app.UseAuthorization();

            app.UseResponseCompression();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapControllers(); // API route
            });
        }
    }
}
