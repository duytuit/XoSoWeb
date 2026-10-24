using System.Text.Json;
using Microsoft.Data.SqlClient;
using XoSoWeb.Models;
using XoSoWeb.Services;
using XoSoWeb.Controllers;
using XoSoWeb.Middlewares;
using XoSoWeb.Shares.BaseRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using AngleSharp.Html.Parser;

public static class DatabaseAudit
{
    public static async Task<int> RunHttpAsync(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out _)) { Console.WriteLine("Supply --origin with the currently active public origin."); return 2; }
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(root.FullName, "XoSoWeb.csproj"))) root = root.Parent ?? throw new Exception("Repository root missing");
        var configuration = new ConfigurationBuilder().SetBasePath(root.FullName).AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
        var site = new SiteSettings { SiteUrl = origin };
        site.Validate(false);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = root.FullName, WebRootPath = Path.Combine(root.FullName, "wwwroot") });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(site);
        builder.Services.AddMemoryCache();
        builder.Services.AddDbContext<XoSoDBContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddSingleton<ArchiveCatalog>();
        builder.Services.AddScoped<VietlottStatisticsService>();
        builder.Services.AddScoped<NorthernQuickStatisticsService>();
        builder.Services.AddScoped<INewsArticleService, NewsArticleService>();
        builder.Services.AddAuthentication("audit").AddCookie("audit");
        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
        await using var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseMiddleware<PublicUrlMiddleware>();
        app.UseStaticFiles();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        var catalog = await app.Services.GetRequiredService<ArchiveCatalog>().GetAsync();
        var paths = new List<string> { "/", "/xo-so-mien-bac", "/xo-so-mien-trung", "/xo-so-mien-nam", "/thong-ke", "/thong-ke/soi-cau", "/thong-ke/nhip-so-mien-bac", "/thong-ke-tan-suat-loto", "/tin-tuc", "/gioi-thieu", "/nguon-du-lieu", "/lich-quay", "/co-cau-giai-thuong" };
        paths.AddRange(catalog.Where(l => l.Region != null && l.Date.HasValue).GroupBy(l => l.Region).Select(g => g.First().Path));
        paths.AddRange(catalog.Where(l => l.ProvinceCode != null && !l.Date.HasValue).Take(3).Select(l => l.Path));
        paths.AddRange(catalog.Where(l => l.GameCode != null).GroupBy(l => l.GameCode).SelectMany(g => g.Where(l => !l.Date.HasValue).Take(1).Concat(g.Where(l => l.Date.HasValue).Take(1))).Select(l => l.Path));
        using (var scope = app.Services.CreateScope())
        {
            var news = scope.ServiceProvider.GetRequiredService<INewsArticleService>();
            paths.AddRange((await news.GetLatestAsync(2, default)).Select(a => "/tin-tuc/bai-viet/" + a.Slug));
            var index = await scope.ServiceProvider.GetRequiredService<VietlottStatisticsService>().GetIndexAsync(default);
            if (index.Games.Count > 0) paths.Add("/thong-ke/vietlott");
        }
        var failures = 0; var checks = 0;
        var parser = new HtmlParser();
        foreach (var path in paths.Distinct())
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            using var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            var document = parser.ParseDocument(html);
            var valid = (int)response.StatusCode == 200 && document.QuerySelectorAll("h1").Length == 1
                && document.QuerySelector("link[rel=canonical]")?.GetAttribute("href") == site.Absolute(path);
            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
                using (JsonDocument.Parse(script.TextContent)) { }
            checks++; if (!valid) failures++;
            Console.WriteLine($"{(valid ? "PASS" : "FAIL")} HTTP {(int)response.StatusCode}: {path}; {timer.ElapsedMilliseconds}ms; {response.Content.Headers.ContentLength ?? System.Text.Encoding.UTF8.GetByteCount(html)} bytes (local lab, real database)");
        }
        using (var response = await client.GetAsync("/sitemap.xml"))
        {
            var sitemap = System.Xml.Linq.XDocument.Parse(await response.Content.ReadAsStringAsync());
            XNamespaceCheck(sitemap);
            var locations = sitemap.Descendants(System.Xml.Linq.XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9")).Select(l => new Uri(l.Value)).ToList();
            if (sitemap.Root.Name.LocalName == "sitemapindex")
            {
                var pages = new List<Uri>();
                foreach (var child in locations.Take(2))
                {
                    var childXml = System.Xml.Linq.XDocument.Parse(await client.GetStringAsync(child.PathAndQuery));
                    XNamespaceCheck(childXml);
                    pages.AddRange(childXml.Descendants(System.Xml.Linq.XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9")).Select(l => new Uri(l.Value)));
                }
                locations = pages;
            }
            foreach (var location in locations.Take(40))
            {
                using var pageResponse = await client.GetAsync(location.PathAndQuery);
                var document = parser.ParseDocument(await pageResponse.Content.ReadAsStringAsync());
                var robots = document.QuerySelector("meta[name=robots]")?.GetAttribute("content");
                var valid = pageResponse.StatusCode == System.Net.HttpStatusCode.OK && robots != null && !robots.Contains("noindex");
                checks++; if (!valid) failures++;
                Console.WriteLine($"{(valid ? "PASS" : "FAIL")} sitemap sample: {location.AbsolutePath}");
            }
        }
        foreach (var path in new[] { "/page-does-not-exist", "/xo-so-mien-bac/31-02-2026", "/xo-so-mien-bac/01-01-2099", "/vietlott/mega645/ky/not-a-draw" })
        {
            using var response = await client.GetAsync(path); checks++; if ((int)response.StatusCode != 404) failures++;
            Console.WriteLine($"{((int)response.StatusCode == 404 ? "PASS" : "FAIL")} invalid URL {(int)response.StatusCode}: {path}");
        }
        await app.StopAsync();
        Console.WriteLine($"READ-ONLY local HTTP audit: {checks} checks, {failures} failures. No production deployment, writes, analytics, chat/hub, collectors or notifications started.");
        return failures == 0 ? 0 : 1;
    }
    private static void XNamespaceCheck(System.Xml.Linq.XDocument sitemap)
    {
        if (sitemap.Root?.Name.NamespaceName != "http://www.sitemaps.org/schemas/sitemap/0.9") throw new Exception("Invalid sitemap namespace");
    }
    public static async Task<int> RunAsync()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(root.FullName, "XoSoWeb.csproj"))) root = root.Parent ?? throw new Exception("Repository root missing");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var config = new ConfigurationBuilder().SetBasePath(root.FullName).AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings." + environment + ".json", true).AddEnvironmentVariables().Build();
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString)) { Console.WriteLine("Database audit unavailable: DefaultConnection is not configured."); return 2; }
        try
        {
            var connectionOptions = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
            await using var connection = new SqlConnection(connectionOptions.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 15;
            command.CommandText = @"SELECT TOP (10000) Id, Region, ProvinceCode, ProvinceName, DrawDate, Prizes, UpdatedAt
                FROM dbo.XoSoResults WHERE DrawDate < @tomorrow ORDER BY DrawDate DESC, Id DESC";
            command.Parameters.AddWithValue("@tomorrow", LotteryUrls.Today.AddDays(1));
            var rows = new List<XoSoResult>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    rows.Add(new XoSoResult { Id = reader.GetInt64(0), Region = reader.GetString(1), ProvinceCode = reader.GetString(2),
                        ProvinceName = reader.GetString(3), DrawDate = reader.GetDateTime(4),
                        Prizes = ArchiveCatalog.ReadPrizes(reader.IsDBNull(5) ? "" : reader.GetString(5)), UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6) });
            }
            var incompleteBefore = rows.Count(r => !LotteryDataQuality.IsComplete(r.Prizes, LotteryUrls.RegionCode(r.Region)));
            var aliasRows = rows.Count(r => StationCatalog.Resolve(r.ProvinceCode, r.ProvinceName) is var station && station != null
                && (r.ProvinceCode != station.Id || r.ProvinceName != station.Name));
            var merged = StationCatalog.Merge(rows);
            var incomplete = merged.Where(r => !LotteryDataQuality.IsComplete(r.Prizes, LotteryUrls.RegionCode(r.Region))).ToList();
            Console.WriteLine(JsonSerializer.Serialize(new {
                mode = "READ ONLY; newest 10000 regional source rows; no writes or collection requests",
                sourceRows = rows.Count, aliasedSourceRows = aliasRows, structurallyIncompleteSourceRows = incompleteBefore,
                mergedDraws = merged.Count, structurallyIncompleteMergedDraws = incomplete.Count,
                firstDate = rows.Count == 0 ? (DateTime?)null : rows.Min(r => r.DrawDate),
                lastDate = rows.Count == 0 ? (DateTime?)null : rows.Max(r => r.DrawDate),
                incompleteExamples = incomplete.Take(10).Select(r => new { region = LotteryUrls.RegionCode(r.Region), date = r.DrawDate.ToString("yyyy-MM-dd"),
                    path = LotteryUrls.ProvincePath(r.ProvinceName) + "/" + r.DrawDate.ToString("dd-MM-yyyy"),
                    invalidPrizeGroups = r.Prizes.Where(p => !LotteryDataQuality.IsCompletePrize(p, LotteryUrls.RegionCode(r.Region)))
                        .Select(p => new { name = XoSoPrize.NormalizeName(p.PrizeName), count = p.Numbers?.Count ?? 0,
                            widths = p.Numbers?.Select(n => n?.Length ?? 0).Distinct().ToArray() }) })
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (SqlException exception) { Console.WriteLine("Database audit unavailable: SQL error " + exception.Number + ". Connection details were omitted."); return 2; }
    }
}
