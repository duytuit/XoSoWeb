using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using XoSoWeb.Models;
using XoSoWeb.Services;
using Microsoft.AspNetCore.Http;

void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS: " + label); }
var date = new DateTime(2026, 10, 24);
SoiCauDraw Draw(int age, string hit = "12") => new() { Date = date.AddDays(-age), Digits = "12" + new string('0', 105), Loto = new() { hit }, Special = "99" };
var model = new SoiCauViewModel { Date = date, Length = 2, Q = "12", Reverse = false, Draws = new() { Draw(1), Draw(2), Draw(3) } };
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Single().Steps.Count == 2, "two historical transitions, prediction day excluded");
model.Q = "21";
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Count == 0, "ordered positions preserve direct orientation");
model.Draws.ForEach(d => d.Loto = new() { "21" });
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Single().First == 1, "direct mode considers both position orders");
model.Q = "12";
model.Draws[1].Loto = new() { "12" };
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Count == 0, "direct mode rejects reverse hit");
model.Reverse = true;
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Single().Steps[0].Matched == "21", "reverse mode records actual hit");
model.Target = "special";
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Count == 0, "special mode does not count other prizes");
model.Target = "loto";
model.Draws.RemoveAt(1);
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Count == 0, "missing day breaks chain");
model.Draws = new() { Draw(2), Draw(3), Draw(4) };
SoiCauCalculator.Calculate(model);
Check(model.Patterns.Count == 0, "missing immediately preceding draw prevents stale prediction");
var counts = new[] {1,1,2,6,4,6,3,4}; var widths = new[] {5,5,5,5,4,4,3,2};
var prizes = counts.Select((count,i) => new XoSoPrize { PrizeName = i == 0 ? "DB" : i.ToString(), Numbers = Enumerable.Repeat(new string('0', widths[i]),count).ToList() }).Reverse().ToList();
var complete = SoiCauCalculator.CreateDraw(date, prizes);
Check(complete.Digits.Length == 107 && complete.Labels.Count == 107 && complete.Special == "00", "stable prize order and leading zeroes");
prizes[0].Numbers.RemoveAt(0);
Check(SoiCauCalculator.CreateDraw(date, prizes) == null, "incomplete draw rejected");
Check(PublicQueryPolicy.IsValid("/thong-ke/soi-cau", new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["date"] = "2026-10-24", ["length"] = "5", ["target"] = "loto", ["reverse"] = "true", ["q"] = "" })), "filter query passes public middleware");
Console.WriteLine("All SoiCau regression checks passed.");

var fixture = new SoiCauViewModel { Date = date, Length = 2, Q = "00" };
var zeros = counts.Select((count, i) => new XoSoPrize { PrizeName = i == 0 ? "DB" : i.ToString(), Numbers = Enumerable.Repeat(new string('0', widths[i]), count).ToList() }).ToList();
fixture.Draws = Enumerable.Range(1, 4).Select(age => SoiCauCalculator.CreateDraw(date.AddDays(-age), zeros)).ToList();
fixture.Patterns = Enumerable.Range(0, 2).Select(i => new SoiCauPattern {
    First = i, Second = i + 1, Number = "00",
    Steps = Enumerable.Range(1, i + 2).Select(age => new SoiCauStep { SourceDate = date.AddDays(-age - 1), ResultDate = date.AddDays(-age), Number = "00", Matched = "00" }).ToList()
}).ToList();
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (!File.Exists(Path.Combine(root.FullName, "XoSoWeb.csproj"))) root = root.Parent ?? throw new Exception("Repository root missing");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = root.FullName, WebRootPath = Path.Combine(root.FullName, "wwwroot") });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddSingleton(new SiteSettings { SiteUrl = "https://example.org" });
builder.Services.AddSingleton(fixture);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(XoSoWeb.Controllers.HomeController).Assembly);
await using var app = builder.Build();
app.UseDeveloperExceptionPage(); app.UseStaticFiles(); app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
var response = await client.GetAsync("/__soicau-fixture");
var html = await response.Content.ReadAsStringAsync();
Check(response.IsSuccessStatusCode, "Razor soi cau fixture renders successfully");
var document = new HtmlParser().ParseDocument(html);
Check(document.QuerySelectorAll(".sc-position-card .sc-board-digit").Length == 107, "latest draw is displayed in the full interactive prize board");
Check(document.QuerySelectorAll(".sc-position-card .has-pattern:not(:disabled)").Length == 6, "active positions available in board and position map");
Check(document.QuerySelectorAll(".sc-length-nav a").Length == 10 && document.QuerySelector(".sc-length-nav a.is-active").TextContent == "2", "quick lengths include the active threshold");
Check(document.QuerySelectorAll(".sc-number-link").Length == 2 && document.QuerySelectorAll(".sc-number-link.is-long").Length == 1, "each pattern has a link and longer patterns are emphasized");
Check(document.QuerySelectorAll(".sc-pattern").Length == 2 && document.QuerySelector("#sc-pattern-1") != null, "summary links target unique history panels");
Check(document.QuerySelectorAll(".sc-group").Length == 1 && document.QuerySelector(".sc-group").TextContent.Contains("2 cầu"), "repeated patterns are grouped without losing counts");
Check(document.QuerySelector(".sc-length-nav a").GetAttribute("href").Contains("q=00") && document.QuerySelector(".sc-length-nav a").GetAttribute("href").Contains("reverse=true"), "quick length navigation preserves other filters");
if (Environment.GetEnvironmentVariable("SOICAU_PREVIEW_PATH") is string previewPath) await File.WriteAllTextAsync(previewPath, html);
var emptyHtml = await client.GetStringAsync("/__soicau-fixture?empty=true");
var emptyDoc = new HtmlParser().ParseDocument(emptyHtml);
Check(emptyDoc.QuerySelector(".sc-position-card") == null && emptyDoc.QuerySelector("meta[name=robots]").GetAttribute("content").Contains("noindex"), "missing draw shows empty state and noindex");
Console.WriteLine("All SoiCau rendering checks passed.");

public sealed class SoiCauFixtureController : Controller
{
    private readonly SoiCauViewModel model;
    public SoiCauFixtureController(SoiCauViewModel model) { this.model = model; }
    [HttpGet("/__soicau-fixture")]
    public IActionResult Page(bool empty = false) => View("~/Views/Home/SoiCau.cshtml", empty ? new SoiCauViewModel { Date = model.Date } : model);
}
