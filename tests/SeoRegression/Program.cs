using System.Net.Http.Json;
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
void ValidateRenderedSeo(string html, string path, string origin)
{
    var decoded = System.Net.WebUtility.HtmlDecode(html);
    Check(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]").Count == 1, "single content H1: " + path);
    Check(System.Text.RegularExpressions.Regex.IsMatch(html, "<title>[^<]+</title>") && html.Contains("name=\"description\" content=\"") && !html.Contains("name=\"keywords\""), "title and description without keywords: " + path);
    var scripts = System.Text.RegularExpressions.Regex.Matches(html, "<script type=\"application/ld[+]json\">(.*?)</script>", System.Text.RegularExpressions.RegexOptions.Singleline);
    Check(scripts.Count > 0, "JSON-LD present: " + path);
    foreach (System.Text.RegularExpressions.Match script in scripts)
    {
        using var json = JsonDocument.Parse(script.Groups[1].Value);
        Check(json.RootElement.GetProperty("@context").GetString() == "https://schema.org", "JSON-LD context: " + path);
        foreach (var node in json.RootElement.GetProperty("@graph").EnumerateArray())
        {
            Check(node.TryGetProperty("@type", out var type) && !node.TryGetProperty("type", out _), "JSON-LD node type: " + path);
            if (type.GetString() != "BreadcrumbList") continue;
            var canonicalMatch = System.Text.RegularExpressions.Regex.Match(decoded, "rel=\"canonical\" href=\"([^\"]+)\"");
            Check(node.GetProperty("itemListElement").EnumerateArray().Last().GetProperty("item").GetString() == canonicalMatch.Groups[1].Value, "breadcrumb current URL equals canonical: " + path);
            var position = 0;
            foreach (var crumb in node.GetProperty("itemListElement").EnumerateArray())
            {
                Check(crumb.GetProperty("@type").GetString() == "ListItem" && !crumb.TryGetProperty("type", out _)
                    && crumb.GetProperty("position").GetInt32() == ++position, "breadcrumb ListItem and position: " + path);
                Check(decoded.Contains(crumb.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase) && crumb.GetProperty("item").GetString().StartsWith(origin + "/"), "breadcrumb matches content and origin: " + path + " / " + crumb.GetProperty("name").GetString() + " / " + crumb.GetProperty("item").GetString());
            }
        }
    }
}
void RejectOrigin(string url)
{
    try { new SiteSettings { SiteUrl = url }.Validate(false); }
    catch (InvalidOperationException) { checks++; return; }
    throw new Exception("Accepted invalid production SiteUrl: " + url);
}
foreach (var alias in new[] { "Giải 8", "G8", "8", "giải tám", " G.8 " })
    Check(XoSoPrize.NormalizeName(alias) == "8", "eighth prize alias: " + alias);
foreach (var alias in new[] { "ĐB", "DB", "Giải Đặc Biệt", "Giai DB", "G.ĐB" })
    Check(XoSoPrize.NormalizeName(alias) == "ĐB", "special prize alias: " + alias);
Check(XoSoPrize.NormalizeName("Unknown prize") == "Unknown prize", "unrecognized prize labels preserved");
foreach (var alias in new[] { "Giải 1", "G1", "nhất", "Giai nhat" })
    Check(XoSoPrize.DisplayName(alias) == "G1", "consistent numbered prize label: " + alias);
Check(XoSoPrize.DisplayName("DB") == "ĐB" && XoSoPrize.DisplayName("Giải tám") == "G8", "consistent special and eighth prize labels");
var northernPrizes = XoSoPrize.NorthernDisplayPrizes(new[]
{
    new XoSoPrize { PrizeName = "Giải ba", Numbers = new List<string> { "00123" } },
    new XoSoPrize { PrizeName = "G1", Numbers = new List<string> { "00456" } },
    new XoSoPrize { PrizeName = "G3", Numbers = new List<string> { "00789" } }
});
Check(northernPrizes.Select(prize => XoSoPrize.DisplayName(prize.PrizeName)).SequenceEqual(new[] { "ĐB", "G1", "G2", "G3", "G4", "G5", "G6", "G7" }), "incomplete northern draw retains all eight rows in order");
Check(northernPrizes[0].Numbers.Count == 0 && northernPrizes[7].Numbers.Count == 0, "missing northern prizes have no invented numbers");
Check(northernPrizes[3].Numbers.SequenceEqual(new[] { "00123", "00789" }), "northern aliases merge without losing leading zeroes");
Check(ArchiveCatalog.HasResultNumbers("[{\"PrizeName\":\"Test\",\"Numbers\":[\"00\"]}]"), "catalog JSON validated without SQL JSON functions");
foreach (var json in new[] { "[]", "null", "{bad", "[null]", "[{\"PrizeName\":\"Test\",\"Numbers\":[]}]" })
    Check(!ArchiveCatalog.HasResultNumbers(json), "invalid or empty catalog data excluded");
foreach (var url in new[] { "http://example.org", "https://localhost", "https://example.org/path", "https://user@example.org", "https://example.org/?x=1", "//example.org" }) RejectOrigin(url);
var site = new SiteSettings { SiteUrl = "https://example.org/", OrganizationName = "Test operator" };
var accessor = new HttpContextAccessor();
var automaticSite = new SiteSettings(accessor);
automaticSite.Validate(false);
accessor.HttpContext = new DefaultHttpContext();
accessor.HttpContext.Request.Scheme = "https";
accessor.HttpContext.Request.Host = new HostString("first.example.org");
Check(automaticSite.Absolute("/sitemap.xml") == "https://first.example.org/sitemap.xml", "automatic origin uses current request");
accessor.HttpContext = new DefaultHttpContext();
accessor.HttpContext.Request.Scheme = "http";
accessor.HttpContext.Request.Host = new HostString("localhost", 5099);
Check(automaticSite.SiteUrl == "http://localhost:5099", "automatic origin is not retained between requests");
accessor.HttpContext = null;
site.Validate(false);
Check(site.SiteUrl == "https://example.org", "origin normalized");
Check(LotteryUrls.TryDate("29-02-2024", out _), "valid leap day");
Check(!LotteryUrls.TryDate("29-02-2025", out _) && !LotteryUrls.TryDate("31-04-2025", out _), "invalid calendar dates");
Check(!LotteryUrls.TryDate(LotteryUrls.Today.AddDays(1).ToString("dd-MM-yyyy"), out _), "future date rejected in UTC+7");
Check(!LotteryUrls.TryDate("1-1-2024", out _), "strict date format");
Check(LotteryUrls.ProvincePath("Long An") == "/xo-so-long-an" && LotteryUrls.ProvincePath("Xổ số Đắk Lắk") == "/xo-so-dak-lak", "province slugs from names");
Check(LotteryUrls.DrawPath("MAX3D_PRO", "00019") == "/vietlott/max3d-pro/ky/00019", "draw identifiers retain leading zeroes");
foreach (var alias in new[] { "TPHCM", "TP HCM", "TP.HCM", "Xổ số TPHCM", "tp-hcm" })
    Check(LotteryUrls.ProvincePath(alias) == "/xo-so-tp-hcm", "HCM canonical: " + alias);
Check(LotteryUrls.ProvincePath("Đắc Lắc") == "/xo-so-dak-lak" && LotteryUrls.ProvincePath("Dac Nong") == "/xo-so-dak-nong", "Dak aliases canonicalized");
var mergedStationRows = StationCatalog.Merge(new[] {
    new XoSoResult { Id = 1, ProvinceCode = "TPHCM", ProvinceName = "TPHCM", Region = "MN", DrawDate = new DateTime(2024, 1, 1), SourcePriority = 10,
        Prizes = new() { new() { PrizeName = "G1", Numbers = new() { "00123" } } } },
    new XoSoResult { Id = 2, ProvinceCode = "HCM", ProvinceName = "TP HCM", Region = "MN", DrawDate = new DateTime(2024, 1, 1), SourcePriority = 0,
        Prizes = new() { new() { PrizeName = "Giải 1", Numbers = new() { "99999" } }, new() { PrizeName = "G2", Numbers = new() { "00456" } } } },
    new XoSoResult { Id = 3, ProvinceCode = "HCM", ProvinceName = "TP HCM", Region = "MN", DrawDate = new DateTime(2023, 1, 1) }
});
Check(mergedStationRows.Count == 2 && mergedStationRows.All(r => r.ProvinceCode == "HCM"), "station history unified across aliases and dates");
Check(mergedStationRows[0].Prizes[0].Numbers.SequenceEqual(new[] { "00123" }) && mergedStationRows[0].Prizes.Count == 2, "source priority resolves conflicts and retains missing prizes");
Check(ArchiveCatalog.HasVietlottContent("KENO", Enumerable.Range(1, 20).Select(n => n.ToString("00")), false), "complete Keno accepted");
Check(!ArchiveCatalog.HasVietlottContent("KENO", Enumerable.Repeat("01", 20), false), "duplicate Keno numbers rejected");
Check(ArchiveCatalog.HasVietlottContent("BINGO18", new[] { "1", "1", "6" }, false) && !ArchiveCatalog.HasVietlottContent("BINGO18", new[] { "1", "7", "6" }, false), "Bingo dice completeness and range");
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
    Check(realCatalog.Count(x => x.Path == "/xo-so-tp-hcm/17-10-2024") == 1 && !realCatalog.Any(x => x.Path.StartsWith("/xo-so-tphcm")), "catalog and sitemap emit only canonical station history");
    Check(!realCatalog.Any(x => x.DrawNo == "00999" || x.DrawNo == "00998"), "incomplete Keno and Bingo excluded from sitemap catalog");
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
var firstSitemap = XDocument.Parse(((ContentResult)await controller.Sitemap(1)).Content).Descendants(ns + "loc").Select(x => x.Value).ToList();
var secondSitemap = XDocument.Parse(((ContentResult)await controller.Sitemap(2)).Content).Descendants(ns + "loc").Select(x => x.Value).ToList();
Check(!firstSitemap.Intersect(secondSitemap).Any() && secondSitemap.Distinct().Count() == secondSitemap.Count, "no duplicates within or between sitemap partitions");
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
var contactHandler = new ContactTestHandler();
var contactConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
    ["TelegramContact:BotToken"] = "fixture-token", ["TelegramContact:ChatId"] = "fixture-chat"
}).Build();
var contactService = new TelegramContactService(contactConfig, Microsoft.Extensions.Logging.Abstractions.NullLogger<TelegramContactService>.Instance, new HttpClient(contactHandler));
builder.Services.AddSingleton(contactService);

builder.Services.AddDbContext<XoSoDBContext>(o => o.UseSqlServer(new HistoryConnection()));
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
await using var app = builder.Build();
app.UseMiddleware<PublicUrlMiddleware>();
app.UseStaticFiles();
app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
using (var response = await client.GetAsync("/xo-so-mien-trung/17-10-2024"))
{
    Check((int)response.StatusCode == 200, "central results render with mixed prize names");
    var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    var tableStart = html.IndexOf("<table class=\"regional-results-table\"");
    var tableEnd = html.IndexOf("</table>", tableStart);
    var table = html.Substring(tableStart, tableEnd - tableStart);
    var rows = System.Text.RegularExpressions.Regex.Matches(table, @"<tr\b[^>]*>.*?</tr>", System.Text.RegularExpressions.RegexOptions.Singleline)
        .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Value).ToList();
    Check(rows.Count == 4, "mixed prize names produce three prize rows and one header");
    Check(rows[1].Contains(">G8</th>") && rows[1].Contains(">01</span>") && rows[1].Contains(">02</span>"), "eighth prize aligned across provinces");
    Check(rows[2].Contains(">G4</th>") && rows[2].Contains(">00123</span>") && rows[2].Contains(">00456</span>"), "word and numeric prize names share a row");
    Check(rows[3].Contains(">ĐB</th>") && rows[3].Contains(">001234</span>") && rows[3].Contains(">005678</span>"), "special prize last with leading zeroes intact");
}
links.Add(new ArchiveLink { Path = "/vietlott/mega645", GameCode = "MEGA645", Label = "Mega 6/45" });
foreach (var path in new[] { "/", "/xo-so-mien-bac", "/xo-so-mien-trung", "/xo-so-mien-nam", "/xo-so-mien-bac?day=1", "/xo-so-fixture-station", "/xo-so-fixture-station/17-10-2024", "/vietlott", "/vietlott/mega645", "/vietlott/mega645/ky/00019", "/vietlott?gameCode=MEGA645&offset=1", "/thong-ke", "/thong-ke?endDate=2024-10-17", "/thong-ke-tan-suat-loto", "/thong-ke-tan-suat-loto?region=MN&week=2024-10-17" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 200, "SEO template returns 200: " + path);
    var html = await response.Content.ReadAsStringAsync();
    ValidateRenderedSeo(html, path, site.SiteUrl);
    Check(System.Net.WebUtility.HtmlDecode(html).Contains("href=\"" + site.Absolute(path) + "\""), "self canonical: " + path);
    if (path.Contains('?')) Check(html.Contains("noindex,follow"), "filters noindex and crawlable: " + path);
    if (path.Contains("/ky/")) Check(html.Contains("00019") && html.Contains("17/10/2024"), "draw metadata retains actual date and identifier");
}
foreach (var path in new[] { "/xo-so-mien-bac?day=1&day=2", "/xo-so-mien-bac?offset=3", "/vietlott?offset=01&gameCode=MEGA645", "/vietlott?offset=1&gameCode=KENO", "/thong-ke?endDate=2024-10-17T00:00:00", "/?arbitrary=1", "/xo-so-fixture-station/17-10-2024?day=1" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 400, "parameter combinations bounded: " + path);
}
using (var response = await client.GetAsync("/vietlott/mega645/ky/not-a-draw")) Check((int)response.StatusCode == 404, "malformed draw is 404");
using (var response = await client.GetAsync("/robots.txt")) Check(!(await response.Content.ReadAsStringAsync()).Contains("Disallow:"), "production robots permits reading noindex filters");
using (var response = await client.GetAsync("/sitemap-1.xml")) Check((int)response.StatusCode == 200 && response.Headers.CacheControl.MaxAge == TimeSpan.FromSeconds(120), "child sitemap returns 200 and is cached");
foreach (var pair in new[] { ("tphcm", "tp-hcm"), ("dac-lac", "dak-lak"), ("dac-nong", "dak-nong") })
{
    foreach (var suffix in new[] { "", "/17-10-2024" })
    {
        using var response = await client.GetAsync("/xo-so-" + pair.Item1 + suffix + "?utm_source=test");
        Check((int)response.StatusCode == 301 && response.Headers.Location?.OriginalString == "/xo-so-" + pair.Item2 + suffix + "?utm_source=test", "station alias permanently redirects with date and query: " + pair.Item1 + suffix);
    }
}
foreach (var path in new[] { "/robots.txt", "/sitemap.xml", "/gioi-thieu", "/nguon-du-lieu", "/support", "/privacy", "/terms", "/images/share.png" })
{
    using var response = await client.GetAsync(path);
    Check((int)response.StatusCode == 200, "HTTP 200 " + path);
    if (response.Content.Headers.ContentType?.MediaType == "text/html")
    {
        var html = await response.Content.ReadAsStringAsync();
        Check(System.Text.RegularExpressions.Regex.Matches(html, "<h1[ >]").Count == 1, "one H1 " + path);
        Check(System.Text.RegularExpressions.Regex.Matches(html, "rel=\"canonical\"").Count == 1 && html.Contains(site.SiteUrl + path), "rendered canonical " + path);
        ValidateRenderedSeo(html, path, site.SiteUrl);
        Check(html.Contains("og:url") && html.Contains("application/ld+json"), "rendered SEO " + path);
    }
}
using (var response = await client.GetAsync("/sitemap.xml"))
{
    var sitemap = XDocument.Parse(await response.Content.ReadAsStringAsync());
    foreach (var loc in sitemap.Descendants(ns + "loc").Select(n => n.Value))
    {
        var url = new Uri(loc);
        using var pageResponse = await client.GetAsync(url.PathAndQuery);
        Check((int)pageResponse.StatusCode == 200, "sitemap URL returns 200 without redirect: " + url.AbsolutePath);
        var html = await pageResponse.Content.ReadAsStringAsync();
        Check(!html.Contains("noindex,follow") && System.Net.WebUtility.HtmlDecode(html).Contains("href=\"" + loc + "\""), "sitemap URL is indexable and canonical: " + url.AbsolutePath);
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

// Exercise real CSRF validation, MVC model validation and delivery-confirmation responses.
var supportHtml = await client.GetStringAsync("/support");
var csrf = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(supportHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
Check(!string.IsNullOrEmpty(csrf), "support form provides antiforgery token");
var report = new ContactSupportRequest { FullName = "Fixture reporter", Email = "reporter@example.org", Subject = "Incorrect result", Content = "URL /xo-so-mien-bac/17-10-2024; draw date 17/10/2024; prize DB; source reference supplied by reporter." };
async Task<HttpResponseMessage> PostReport(ContactSupportRequest value, bool withToken = true)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/support") { Content = JsonContent.Create(value) };
    if (withToken) request.Headers.Add("RequestVerificationToken", csrf);
    return await client.SendAsync(request);
}
using (var response = await PostReport(report, false)) Check((int)response.StatusCode == 400 && contactHandler.Calls == 0, "support rejects missing CSRF without delivery");
using (var response = await PostReport(new ContactSupportRequest { Email = "invalid" })) Check((int)response.StatusCode == 400 && contactHandler.Calls == 0, "support rejects invalid report without delivery");
using (var response = await PostReport(report))
{
    using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Check((int)response.StatusCode == 200 && result.RootElement.GetProperty("success").GetBoolean(), "valid error report succeeds only after delivery confirmation");
}
foreach (var failure in new[] { (System.Net.HttpStatusCode.OK, "{\"ok\":false}"), (System.Net.HttpStatusCode.BadGateway, "{\"ok\":true}"), (System.Net.HttpStatusCode.OK, "not JSON") })
{
    contactHandler.Status = failure.Item1; contactHandler.Payload = failure.Item2;
    using var response = await PostReport(report);
    using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Check((int)response.StatusCode == 502 && !result.RootElement.GetProperty("success").GetBoolean(), "unconfirmed delivery is never success: " + failure.Item2);
}
contactHandler.FailNetwork = true;
using (var response = await PostReport(report)) Check((int)response.StatusCode == 502, "network failure returns delivery error");
var missingContact = new TelegramContactService(new ConfigurationBuilder().Build(), Microsoft.Extensions.Logging.Abstractions.NullLogger<TelegramContactService>.Instance, new HttpClient(contactHandler));
var callsBeforeMissingConfig = contactHandler.Calls;
Check(!await missingContact.SendAsync(report, CancellationToken.None) && contactHandler.Calls == callsBeforeMissingConfig, "missing delivery configuration fails without network access");
using (var json = JsonDocument.Parse(SeoMetadata.Create(new SiteSettings { SiteUrl = site.SiteUrl }, context.Request, viewData).StructuredJson))
    Check(!json.RootElement.GetProperty("@graph").EnumerateArray().Any(n => n.GetProperty("@type").GetString() == "Organization"), "organization schema omitted without an operator name");
var footer = System.Net.WebUtility.HtmlDecode(supportHtml);
Check(footer.Contains("Cập nhật kết quả xổ số nhanh chóng — dễ xem, dễ tra cứu mỗi ngày.") && footer.Contains("Chúng tôi liên tục cải thiện tốc độ cập nhật và độ ổn định"), "footer uses requested copy");
var sourcesHtml = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/nguon-du-lieu"));
Check(sourcesHtml.Contains("Website tổng hợp và lưu trữ kết quả") && sourcesHtml.Contains("thông báo chính thức"), "sources retain aggregation and verification explanation");
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
