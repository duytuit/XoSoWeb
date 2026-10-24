using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using XoSoWeb.Controllers;
using XoSoWeb.Models;
using XoSoWeb.Services;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception("FAIL: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
var end = new DateTime(2026, 10, 24);
NumberRhythmDay Day(int age, int count = 0, int special = 99)
{
    var day = new NumberRhythmDay { Date = end.AddDays(-age), HasData = true, Special = special };
    day.Counts[12] = count;
    return day;
}
var draws = new[] { Day(0), Day(1), Day(2, 2, 12), Day(3), Day(4, 1), Day(6, 1) };
var model = new NumberRhythmViewModel { EndDate = end, Days = 7 };
NumberRhythmCalculator.Calculate(model, draws);
var twelve = model.Columns.Single(c => c.Number == 12);
Check(model.Columns.Count == 100 && model.Rows.Count == 7 && model.DrawCount == 6, "all 100 pairs and every calendar day retained");
Check(!model.Rows[5].HasData && model.Rows[0].HasData, "missing day distinguished from known zero-hit day");
Check(twelve.Total == 4 && twelve.HitDays == 3, "multiple hits count separately from hit days");
Check(twelve.Gap == 2 && twelve.GapExact && twelve.LastSeen == end.AddDays(-2), "current gap counts fully observed days since latest hit");
Check(twelve.Cycles.Count == 1 && twelve.Cycles[0].MissDays == 1, "cycles do not bridge missing days");
Check(!model.Columns[0].GapExact && model.Columns[0].Gap == 5, "unseen number gap is lower bound and stops at missing data");
model.Target = "special";
NumberRhythmCalculator.Calculate(model, draws);
Check(model.Columns.Single(c => c.Number == 12).Total == 1, "special mode excludes hits in other prizes");
model.Target = "loto"; model.Sort = "frequency";
NumberRhythmCalculator.Calculate(model, draws);
Check(model.Columns[0].Number == 12, "frequency sort puts greatest total first");
model.Sort = "gap";
NumberRhythmCalculator.Calculate(model, draws);
Check(model.Columns[0].Gap == 5, "gap sort uses confirmed consecutive lower bound");
model.Q = "00,12,12";
NumberRhythmCalculator.Calculate(model, draws.Concat(new[] { Day(-1, 99), Day(10, 99) }));
Check(model.Columns.Count == 2 && model.Columns.Single(c => c.Number == 12).Total == 4, "pair filter deduplicates and excludes out-of-range results");
NumberRhythmCalculator.Calculate(model, draws.Where(d => d.Date != end));
Check(model.Columns.All(c => c.Gap == 0 && !c.GapExact), "missing end date cannot claim an exact current gap");

var counts = new[] { 1, 1, 2, 6, 4, 6, 3, 4 };
var widths = new[] { 5, 5, 5, 5, 4, 4, 3, 2 };
var prizes = counts.Select((count, i) => new XoSoPrize { PrizeName = i == 0 ? "DB" : "G" + i, Numbers = Enumerable.Repeat(new string('0', widths[i]), count).ToList() }).ToList();
prizes[0].Numbers[0] = "00012";
prizes[7].Numbers[0] = "12";
var parsed = NumberRhythmCalculator.CreateDay(end, prizes);
Check(parsed.Counts.Sum() == 27 && parsed.Counts[12] == 2 && parsed.Counts[0] == 25 && parsed.Special == 12, "complete draw preserves leading zeroes and repeated hits");
prizes[7].Numbers.RemoveAt(0);
Check(NumberRhythmCalculator.CreateDay(end, prizes) == null, "incomplete northern draw excluded from counts");
Check(NumberRhythmCalculator.CreateDay(end, null) == null, "null draw excluded");
var policy = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> {
    ["endDate"] = "2026-10-24", ["days"] = "30", ["target"] = "loto", ["sort"] = "frequency", ["q"] = "00,12"
});
Check(PublicQueryPolicy.Applies("/thong-ke/nhip-so-mien-bac") && PublicQueryPolicy.IsValid("/thong-ke/nhip-so-mien-bac", policy), "public middleware accepts new page and filters");
var controller = new HomeController(null, null, null);
Check(await controller.NumberRhythm(days: 0) is BadRequestResult && await controller.NumberRhythm(days: 121) is BadRequestResult, "day bounds rejected before database access");
Check(await controller.NumberRhythm(q: "abc") is BadRequestResult && await controller.NumberRhythm(q: ",,") is BadRequestResult, "invalid pair input rejected");
Check(await controller.NumberRhythm(target: "other") is BadRequestResult && await controller.NumberRhythm(sort: "other") is BadRequestResult, "unsupported modes and sorts rejected");
Check(await controller.NumberRhythm(endDate: LotteryUrls.Today.AddDays(1)) is BadRequestResult, "future end date rejected");

IEnumerable<NumberRhythmDay> QuickDraws(IEnumerable<NumberRhythmDay> source) => source.Select(d => new NumberRhythmDay {
    Date = d.Date, HasData = d.HasData, Special = d.Special,
    Counts = Enumerable.Range(0, 100).Select(n => n == 12 || n == 21 ? d.Counts[12] : d.Date == end ? 1 : 0).ToArray()
});
var quick = NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), QuickDraws(draws.Concat(new[] { Day(-1, 99), Day(4000, 99) })));
Check(quick.DrawCount == 6 && quick.MonthDrawCount == 6 && quick.EndDate == end, "quick statistics exclude prediction day and history beyond bound");
Check(quick.Frequent.Single(c => c.Number == 12).Count == 4, "quick frequency counts repeated hits in last 30 calendar days");
Check(quick.LotoGaps.Single(c => c.Number == 12).Days == 2 && quick.LotoGaps.Single(c => c.Number == 12).Exact, "quick current gap ends at actual prior hit");
Check(quick.Sums.Single(c => c.Number == 3).Days == 2 && quick.Sums.Single(c => c.Number == 8).Days == 0, "special sum uses last digit of sum of final two digits");
Check(quick.Touches.Single(c => c.Number == 1).Days == 2 && quick.Touches.Single(c => c.Number == 2).Days == 2, "special touch checks either digit");
Check(quick.LotoGaps.All(c => c.Days > 0) && quick.LotoGaps.Count <= 30, "quick gap ranking has no zero gaps and is bounded");
Check(quick.ReversePairs.All(p => p.First.Number / 10 == p.Second.Number % 10 && p.First.Number % 10 == p.Second.Number / 10 && p.First.Number != p.Second.Number), "reverse pair ranking excludes doubles and duplicate pairs");
var gapsDraws = new[] { Day(0, 1), Day(1), Day(2), Day(3, 1) };
var closedRun = NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), gapsDraws);
var gap12 = closedRun.Leaders.FirstOrDefault(c => c.Number == 12);
// 12 is present at the end date, so it should never be listed as currently overdue.
Check(gap12 == null && closedRun.LotoGaps.All(c => c.Number != 12), "current-hit number excluded from overdue list");
var historicalDraws = new[] { Day(0), Day(1, 1), Day(2), Day(3), Day(4), Day(5, 1) };
var historical = NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), QuickDraws(historicalDraws));
var historical12 = historical.LotoGaps.Single(c => c.Number == 12);
Check(historical12.MaxDays == 3 && historical12.MaxExact && historical12.MaxStart == end.AddDays(-4) && historical12.MaxEnd == end.AddDays(-2), "longest observed complete run has correct inclusive dates");
var withMissing = NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), QuickDraws(historicalDraws.Where(d => d.Date != end.AddDays(-3))));
Check(withMissing.LotoGaps.Single(c => c.Number == 12).MaxDays == 1, "historical maximum does not bridge a missing day");
var missingEnd = NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), draws.Where(d => d.Date != end));
Check(missingEnd.LotoGaps.Count == 0 && missingEnd.SpecialGaps.Count == 0, "missing most recent result does not invent overdue rankings");
var thirtyDayDraws = new[] { Day(0, 2), Day(29, 3), Day(30, 50) };
Check(NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), thirtyDayDraws).Frequent.Single(c => c.Number == 12).Count == 5, "30-day frequency uses calendar boundaries exactly");
Check(NorthernQuickStatisticsCalculator.Calculate(end.AddDays(1), Array.Empty<NumberRhythmDay>()).DrawCount == 0, "empty quick statistics are safe");

var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (!File.Exists(Path.Combine(root.FullName, "XoSoWeb.csproj"))) root = root.Parent ?? throw new Exception("Repository root missing");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = root.FullName, WebRootPath = Path.Combine(root.FullName, "wwwroot") });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddSingleton(new SiteSettings { SiteUrl = "https://example.org" });
model.Q = ""; model.Sort = "number";
NumberRhythmCalculator.Calculate(model, draws);
builder.Services.AddSingleton(model);
builder.Services.AddSingleton(quick);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
await using var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.MapControllers();
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
using var client = new HttpClient { BaseAddress = new Uri(address) };
var html = await client.GetStringAsync("/__rhythm-fixture");
var document = new HtmlParser().ParseDocument(html);
Check(document.QuerySelectorAll("h1").Length == 1 && document.QuerySelector("h1").TextContent.Contains("Nhịp số miền Bắc"), "compiled Razor renders the chosen title");
Check(document.QuerySelectorAll(".rhythm-number-row [data-column]").Length == 100 && document.QuerySelectorAll(".rhythm-table tbody tr").Length == 7, "rendered matrix contains all pair columns and calendar dates");
Check(document.QuerySelectorAll(".rhythm-missing-row").Length == 1 && document.QuerySelectorAll(".rhythm-missing-row td").All(td => td.TextContent == "—"), "rendered missing-day cells show explicit placeholder");
Check(document.QuerySelector("tbody .is-special[data-column='12']")?.TextContent == "2", "special-prize highlight preserves total hit count");
Check(document.QuerySelector("tfoot [data-column='00']").TextContent.Contains("≥"), "censored gap shown as lower bound");
Check(document.QuerySelectorAll("a[href='/thong-ke/nhip-so-mien-bac']").Length == 3, "new entry appears in desktop, mobile and sidebar menus");
using (var json = System.Text.Json.JsonDocument.Parse(document.QuerySelector("#rhythm-cycle-data").TextContent))
    Check(json.RootElement.GetArrayLength() == 100 && json.RootElement.EnumerateArray().Single(c => c.GetProperty("number").GetString() == "12").GetProperty("cycles").GetArrayLength() == 1, "dialog receives correctly bounded cycle data");
var emptyHtml = await client.GetStringAsync("/__rhythm-fixture?empty=true");
var emptyDocument = new HtmlParser().ParseDocument(emptyHtml);
Check(emptyDocument.QuerySelector(".rhythm-empty[role='status']") != null && emptyDocument.QuerySelector("meta[name='robots']").GetAttribute("content").Contains("noindex"), "empty dataset has usable empty state and noindex");
var homeResponse = await client.GetAsync("/__home-quick-fixture");
var homeHtml = await homeResponse.Content.ReadAsStringAsync();
if (!homeResponse.IsSuccessStatusCode) throw new Exception(homeHtml);
var homeDocument = new HtmlParser().ParseDocument(homeHtml);
Check(homeDocument.QuerySelectorAll("h1").Length == 1 && homeDocument.QuerySelector("#quick-statistics-title").TextContent.Contains("25/10/2026"), "homepage renders quick statistics with correct cutoff heading");
Check(homeDocument.QuerySelectorAll(".quick-statistics-block").Length == 7 && homeDocument.QuerySelectorAll(".quick-bar-item").Length == 20, "all requested quick-statistics sections and ten sum/touch bars render");
Check(homeDocument.QuerySelectorAll(".quick-statistics a[href*='nhip-so-mien-bac']").Length > 10, "quick numbers link to working number rhythm route");
Check(homeHtml.IndexOf("quick-statistics-title") > homeHtml.IndexOf("home-region-summary-grid") && homeHtml.IndexOf("quick-statistics-title") < homeHtml.IndexOf("home-vietlott-summary"), "quick statistics placed after regional results and before Vietlott");
var blankQuickHtml = await client.GetStringAsync("/__home-quick-fixture?empty=true");
Check(new HtmlParser().ParseDocument(blankQuickHtml).QuerySelector(".quick-statistics-empty").TextContent.Contains("Chưa có kết quả miền Bắc đầy đủ"), "homepage handles empty quick statistics");
await app.StopAsync();
Console.WriteLine($"Completed {checks} NumberRhythm regression checks.");

public sealed class RhythmFixtureController : Controller
{
    private readonly NumberRhythmViewModel model;
    public RhythmFixtureController(NumberRhythmViewModel model) => this.model = model;
    [HttpGet("/__rhythm-fixture")]
    public IActionResult Page(bool empty = false)
    {
        if (!empty) return View("~/Views/Home/NumberRhythm.cshtml", model);
        var blank = new NumberRhythmViewModel { EndDate = model.EndDate, Days = 7 };
        NumberRhythmCalculator.Calculate(blank, Array.Empty<NumberRhythmDay>());
        return View("~/Views/Home/NumberRhythm.cshtml", blank);
    }
}

public sealed class HomeQuickFixtureController : Controller
{
    private readonly NorthernQuickStatisticsViewModel quick;
    public HomeQuickFixtureController(NorthernQuickStatisticsViewModel quick) => this.quick = quick;
    [HttpGet("/__home-quick-fixture")]
    public IActionResult Page(bool empty = false) => View("~/Views/Home/Index.cshtml", new HomePageViewModel {
        MienBac = new(), MienTrung = new(), MienNam = new(), Vietlott = new(),
        NorthernQuickStatistics = empty ? new() : quick
    });
}
