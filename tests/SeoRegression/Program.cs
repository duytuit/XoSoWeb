using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using XoSoWeb.Controllers;
using XoSoWeb.Middlewares;
using XoSoWeb.Models;
using XoSoWeb.Services;
using XoSoWeb.Shares.BaseRepository;

var checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception("FAIL: " + label);
    checks++;
    Console.WriteLine("PASS: " + label);
}
void RejectOrigin(string url)
{
    try { new SiteSettings { SiteUrl = url }.Validate(false); }
    catch (InvalidOperationException) { checks++; return; }
    throw new Exception("Accepted invalid production SiteUrl: " + url);
}
Check(ArchiveCatalog.HasResultNumbers("[{\"PrizeName\":\"Test\",\"Numbers\":[\"00\"]}]"), "catalog JSON validated without SQL JSON functions");
foreach (var json in new[] { "[]", "null", "{bad", "[null]", "[{\"PrizeName\":\"Test\",\"Numbers\":[]}]" })
    Check(!ArchiveCatalog.HasResultNumbers(json), "invalid or empty catalog data excluded");
foreach (var url in new[] { "", "http://example.org", "https://localhost", "https://example.org/path", "https://user@example.org", "https://example.org/?x=1", "//example.org" }) RejectOrigin(url);
var site = new SiteSettings { SiteUrl = "https://example.org/", OrganizationName = "Test operator" };
site.Validate(false);
Check(site.SiteUrl == "https://example.org", "origin normalized");
Check(LotteryUrls.TryDate("29-02-2024", out _), "valid leap day");
Check(!LotteryUrls.TryDate("29-02-2025", out _) && !LotteryUrls.TryDate("31-04-2025", out _), "invalid calendar dates");
Check(!LotteryUrls.TryDate(LotteryUrls.Today.AddDays(1).ToString("dd-MM-yyyy"), out _), "future date rejected in UTC+7");
Check(!LotteryUrls.TryDate("1-1-2024", out _), "strict date format");
Check(LotteryUrls.ProvincePath("Long An") == "/xo-so-long-an" && LotteryUrls.ProvincePath("Xổ số Đắk Lắk") == "/xo-so-dak-lak", "province slugs from names");
Check(LotteryUrls.DrawPath("MAX3D_PRO", "00019") == "/vietlott/max3d-pro/ky/00019", "draw identifiers retain leading zeroes");
var split = typeof(HomeController).GetMethod("SplitValues", BindingFlags.NonPublic | BindingFlags.Static);
Check(((List<string>)split.Invoke(null, new object[] { "01 007,00;12" })).SequenceEqual(new[] { "01", "007", "00", "12" }), "result strings retain leading zeroes");

var context = new DefaultHttpContext();
context.Request.Path = "/xo-so-mien-bac/17-10-2024";
context.Request.Host = new HostString("attacker.invalid");
var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { ["Title"] = "History" };
var seo = SeoMetadata.Create(site, context.Request, viewData);
Check(seo.Canonical == "https://example.org/xo-so-mien-bac/17-10-2024", "historical canonical ignores hostile Host");
using (JsonDocument.Parse(seo.StructuredJson)) { Check(true, "structured data is JSON"); }
context.Request.QueryString = new QueryString("?day=1&utm_source=test");
seo = SeoMetadata.Create(site, context.Request, viewData);
Check(seo.NoIndex && seo.Canonical.EndsWith("?day=1"), "filters noindex with self canonical; tracking removed");
site.SiteUrl = "https://new.example.org";
seo = SeoMetadata.Create(site, context.Request, viewData);
Check(seo.Canonical.StartsWith(site.SiteUrl) && !seo.StructuredJson.Contains("https://example.org"), "domain change updates canonical and schema");

context = new DefaultHttpContext(); context.Request.Method = "GET";
context.Request.Path = "/Home/XoSoMienBac/"; context.Request.QueryString = new QueryString("?day=2");
await new PublicUrlMiddleware(_ => throw new Exception("Redirect not executed")).InvokeAsync(context, site);
Check(context.Response.StatusCode == 301 && context.Response.Headers.Location == site.SiteUrl + "/xo-so-mien-bac?day=2", "legacy alias in a single redirect preserves query");
bool called = false;
context = new DefaultHttpContext(); context.Request.Method = "GET"; context.Request.Path = "/xo-so-mien-bac";
await new PublicUrlMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, site);
Check(called && context.Response.StatusCode == 200, "normalized route has no redirect loop");

var cache = new MemoryCache(new MemoryCacheOptions());
using (var catalogServices = new ServiceCollection().AddMemoryCache()
    .AddDbContext<XoSoDBContext>(o => o.UseSqlServer(new HistoryConnection())).AddSingleton<ArchiveCatalog>().BuildServiceProvider())
{
    var realCatalog = await catalogServices.GetRequiredService<ArchiveCatalog>().GetAsync();
    Check(realCatalog.Count(x => x.Path == "/xo-so-fixture-station") == 1, "province landing survives multiple source codes");
    Check(realCatalog.Count(x => x.Path == "/xo-so-fixture-station/17-10-2024") == 1, "province history deduplicates source codes");
    Check(realCatalog.Any(x => x.Path == "/vietlott/mega645/ky/00019"), "catalog runs through legacy-compatible SQL");
}
// Synthetic navigation fixtures only; these are never written to a result database.
var links = new List<ArchiveLink> { new ArchiveLink { Path = "/xo-so-mien-bac/17-10-2024", Region = "MB", Date = new DateTime(2024, 10, 17), Modified = new DateTime(2024, 10, 18) } };
cache.Set("archive-catalog", links);
var catalog = new ArchiveCatalog(null, cache);
var controller = new SeoController(site, catalog) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
var xml = (ContentResult)await controller.Sitemap();
XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
var document = XDocument.Parse(xml.Content);
var locations = document.Descendants(ns + "loc").Select(x => x.Value).ToList();
Check(xml.ContentType.StartsWith("application/xml") && locations.All(x => x.StartsWith(site.SiteUrl + "/")), "sitemap XML uses configured origin");
Check(locations.Distinct().Count() == locations.Count && !locations.Any(x => x.Contains('?')), "sitemap has no duplicate/filter URLs");
Check(document.Descendants(ns + "lastmod").Single().Value == "2024-10-18", "sitemap lastmod from stored update");
Check(await controller.Sitemap(0) is NotFoundResult, "invalid sitemap partition returns 404");
links.AddRange(Enumerable.Range(1, 10001).Select(n => new ArchiveLink { Path = "/fixture-" + n }));
Check(XDocument.Parse(((ContentResult)await controller.Sitemap()).Content).Root.Name.LocalName == "sitemapindex", "large sitemap splits into index");
Check(XDocument.Parse(((ContentResult)await controller.Sitemap(1)).Content).Descendants(ns + "url").Count() == 10000, "sitemap partition bounded");
links.RemoveRange(1, links.Count - 1);
links.Add(new ArchiveLink { Path = "/vietlott/mega645/ky/00019", GameCode = "MEGA645", DrawNo = "00019", Date = new DateTime(2024, 10, 17), Label = "Mega 6/45 · 00019" });
links.Add(new ArchiveLink { Path = "/xo-so-fixture-station", Region = "MN", ProvinceCode = "FX", ProvinceName = "Fixture station", Label = "Fixture station" });
site.AllowIndexing = false;
Check(((ContentResult)controller.Robots()).Content.Contains("Disallow: /") && !XDocument.Parse(((ContentResult)await controller.Sitemap()).Content).Descendants(ns + "url").Any(), "nonproduction robots and sitemap");
site.AllowIndexing = true;
Check(((ContentResult)controller.Robots()).Content.Contains(site.Absolute("/sitemap.xml")), "production robots advertises sitemap");

// Run real MVC routing and compiled Razor with isolated configuration. No DB or Redis calls.
var rootDirectory = new DirectoryInfo(AppContext.BaseDirectory);
while (!File.Exists(Path.Combine(rootDirectory.FullName, "XoSoWeb.csproj"))) rootDirectory = rootDirectory.Parent ?? throw new Exception("Repository root missing");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = rootDirectory.FullName, WebRootPath = Path.Combine(rootDirectory.FullName, "wwwroot") });
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddSingleton(site);
builder.Services.AddSingleton<IMemoryCache>(cache);
builder.Services.AddSingleton(catalog);
builder.Services.AddDbContext<XoSoDBContext>(o => o.UseSqlServer(new HistoryConnection()));
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
await using var app = builder.Build();
app.UseMiddleware<PublicUrlMiddleware>();
app.UseStaticFiles();
app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
foreach (var path in new[] { "/robots.txt", "/sitemap.xml", "/gioi-thieu", "/nguon-du-lieu", "/support", "/privacy", "/terms", "/images/share.png" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 200, "HTTP 200 " + path);
    if (response.Content.Headers.ContentType?.MediaType == "text/html")
    {
        var html = await response.Content.ReadAsStringAsync();
        Check(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]").Count == 1, "one H1 " + path);
        Check(System.Text.RegularExpressions.Regex.Matches(html, "rel=\"canonical\"").Count == 1 && html.Contains(site.SiteUrl + path), "rendered canonical " + path);
        Check(html.Contains("og:url") && html.Contains("application/ld+json"), "rendered SEO " + path);
    }
}
foreach (var path in new[] { "/unknown-page", "/xo-so-mien-bac/31-02-2024", "/xo-so-mien-bac/01-01-2099", "/vietlott/unknown", "/sitemap-999.xml" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 404, "real HTTP 404 " + path);
}
foreach (var path in new[] { "/xo-so-mien-bac/17-10-2024", "/vietlott/mega645/ky/00019" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 200, "history route renders " + path);
    var html = await response.Content.ReadAsStringAsync();
    Check(html.Contains("17/10/2024") && html.Contains(path) && html.Contains("rel=\"canonical\""), "historical date and canonical survive direct access");
    Check(html.Contains(path.Contains("vietlott") ? ">01</strong>" : ">00123</span>"), "initial result HTML retains zeroes");
    if (path.Contains("vietlott")) Check(html.Contains("001") && html.Contains("007") && html.Contains("Fixture value"), "batched prize rows preserve string numbers and values");
    Check(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]").Count == 1, "one historical H1");
}
foreach (var path in new[] { "/xo-so-mien-bac/16-10-2024", "/vietlott/mega645/ky/00020", "/xo-so-fixture-station/17-10-2024" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 200, "fresh draw bypasses stale navigation catalog " + path);
}
foreach (var path in new[] { "/xo-so-mien-bac/15-10-2024", "/vietlott/mega645/ky/00021" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 404, "missing stored data remains 404 " + path);
}
using (var response = await client.GetAsync("/xo-so-mien-bac?day=8")) Check((int)response.StatusCode == 400, "invalid weekday bounded");
using (var response = await client.GetAsync("/vietlott?gameCode=MEGA645")) Check((int)response.StatusCode == 301 && response.Headers.Location.ToString() == "/vietlott/mega645", "legacy game URL redirect");
using (var response = await client.GetAsync("/Home/Index/")) Check((int)response.StatusCode == 301 && response.Headers.Location.ToString() == site.Absolute("/"), "legacy home routing");
await app.StopAsync();
var stagingEnvironment = app.Services.GetRequiredService<IWebHostEnvironment>();
stagingEnvironment.EnvironmentName = "Staging";
site.StagingUsername = "fixture-user"; site.StagingPassword = "fixture-secret";
context = new DefaultHttpContext();
await new StagingAccessMiddleware(_ => throw new Exception("Staging bypass")).InvokeAsync(context, site, stagingEnvironment);
Check(context.Response.StatusCode == 401 && context.Response.Headers["X-Robots-Tag"].ToString().Contains("noindex"), "staging access requires authentication and noindex");
context = new DefaultHttpContext();
context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("fixture-user:fixture-secret"));
called = false;
await new StagingAccessMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, site, stagingEnvironment);
Check(called, "staging valid credentials allowed");
Console.WriteLine($"Completed {checks} checks.");
