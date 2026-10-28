using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using XoSoWeb.Services;
using Microsoft.Extensions.Caching.Memory;

namespace XoSoWeb.Controllers
{
    public sealed class SeoController : Controller
    {
        private readonly SiteSettings site;
        private readonly ArchiveCatalog catalog;
        private readonly VietlottStatisticsService statistics;
        private readonly INewsArticleService news;
        private readonly IMemoryCache cache;
        public SeoController(SiteSettings site, ArchiveCatalog catalog, VietlottStatisticsService statistics = null, INewsArticleService news = null, IMemoryCache cache = null)
        {
            this.site = site;
            this.catalog = catalog;
            this.statistics = statistics;
            this.news = news;
            this.cache = cache;
        }
        [HttpGet("/robots.txt")]
        public IActionResult Robots()
        {
            Response.Headers["Cache-Control"] = site.AllowIndexing ? "public,max-age=120" : "no-store";
            return Content(site.AllowIndexing
                ? "User-agent: *\nAllow: /\nSitemap: " + site.Absolute("/sitemap.xml") + "\n"
                : "User-agent: *\nDisallow: /\n", "text/plain; charset=utf-8");
        }

        [HttpGet("/sitemap.xml")]
        [HttpGet("/sitemap-{part:int}.xml")]
        public async Task<IActionResult> Sitemap(int? part = null)
        {
            Response.Headers["Cache-Control"] = site.AllowIndexing ? "public,max-age=120" : "no-store";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            if (!site.AllowIndexing) return Content(new XDocument(new XElement(ns + "urlset")).ToString(), "application/xml; charset=utf-8");
            var cacheKey = $"sitemap:{site.SiteUrl}:{PublicContentVersion.Value}:{LotteryUrls.Today:yyyyMMdd}:{part}";
            if (cache != null && cache.TryGetValue(cacheKey, out string cachedXml))
                return Content(cachedXml, "application/xml; charset=utf-8");
            var links = await catalog.GetAsync(HttpContext.RequestAborted);
            links = links.Where(x => x.IsIndexable).ToList();
            var landingPages = links.Where(x => x.Region != null).Select(x => LotteryUrls.RegionPath(x.Region)).Distinct().ToList();
            if (links.Any(x => x.GameCode != null)) landingPages.Add("/vietlott");
            var statisticsPages = new List<string>();
            // Only publish default statistics pages when their actual default date window has data.
            if (links.Any(x => x.Region != null && x.Date >= LotteryUrls.Today.AddDays(-29))) statisticsPages.Add("/thong-ke");
            var weekStart = LotteryUrls.Today.AddDays(-(((int)LotteryUrls.Today.DayOfWeek + 6) % 7));
            if (links.Any(x => x.Region == "MB" && x.Date >= weekStart)) statisticsPages.Add("/thong-ke-tan-suat-loto");
            if (links.Any(x => x.Region == "MB" && x.Date >= LotteryUrls.Today.AddDays(-29))) statisticsPages.Add("/thong-ke/nhip-so-mien-bac");
            if (links.Any(x => x.Region == "MB" && x.Date == LotteryUrls.Today.AddDays(-1))
                && links.Any(x => x.Region == "MB" && x.Date == LotteryUrls.Today.AddDays(-2))) statisticsPages.Add("/thong-ke/soi-cau");
            if (statistics != null)
            {
                var statisticsIndex = await statistics.GetIndexAsync(HttpContext.RequestAborted);
                if (statisticsIndex.Games.Count > 0) statisticsPages.Add("/thong-ke/vietlott");
                foreach (var game in statisticsIndex.Games)
                {
                    var gamePath = "/thong-ke/vietlott/" + game.Slug;
                    statisticsPages.Add(gamePath);
                    statisticsPages.AddRange(game.Tools.Select(tool => gamePath + "/" + tool.Slug));
                }
            }
            var urls = links.Select(x => (x.Path, x.Modified)).Concat(new[] { "/", "/gioi-thieu", "/nguon-du-lieu", "/support", "/privacy", "/terms", "/lien-he-quang-cao" }.Concat(landingPages).Concat(statisticsPages)
                .Select(path => (Path: path, Modified: (DateTime?)null))).GroupBy(x => x.Path, StringComparer.Ordinal).Select(g => (Path: g.Key, Modified: g.Max(x => x.Modified))).OrderBy(x => x.Path, StringComparer.Ordinal).ToList();
            if (news != null)
            {
                var newsCategoryPages = await news.GetPublishedCategoryPathsAsync(HttpContext.RequestAborted);
                for (var offset = 0; ; offset += 1000)
                {
                    var articles = await news.GetSitemapPageAsync(offset, 1000, HttpContext.RequestAborted);
                    urls.AddRange(articles.Select(article =>
                        (Path: "/tin-tuc/bai-viet/" + article.Slug, Modified: (DateTime?)article.ModifiedAt)));
                    if (articles.Count < 1000) break;
                }
                urls.AddRange(newsCategoryPages.Select(path => (Path: path, Modified: (DateTime?)null)));
                urls = urls.GroupBy(item => item.Path, StringComparer.Ordinal)
                    .Select(group => (Path: group.Key, Modified: group.Max(item => item.Modified)))
                    .OrderBy(item => item.Path, StringComparer.Ordinal).ToList();
            }
            const int pageSize = 10000;
            int pages = (urls.Count + pageSize - 1) / pageSize;
            if (part.HasValue && (part < 1 || part > pages)) return NotFound();
            XElement root;
            if (!part.HasValue && pages > 1)
                root = new XElement(ns + "sitemapindex", Enumerable.Range(1, pages).Select(n => new XElement(ns + "sitemap", new XElement(ns + "loc", site.Absolute($"/sitemap-{n}.xml")))));
            else
                root = new XElement(ns + "urlset", urls.Skip(((part ?? 1) - 1) * pageSize).Take(pageSize).Select(x =>
                    new XElement(ns + "url", new XElement(ns + "loc", site.Absolute(x.Path)),
                        x.Modified.HasValue ? new XElement(ns + "lastmod", x.Modified.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null)));
            var xml = new XDocument(root).ToString();
            cache?.Set(cacheKey, xml, TimeSpan.FromMinutes(2));
            return Content(xml, "application/xml; charset=utf-8");
        }
    }
}
