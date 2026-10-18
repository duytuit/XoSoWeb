using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using XoSoWeb.Services;

namespace XoSoWeb.Controllers
{
    public sealed class SeoController : Controller
    {
        private readonly SiteSettings site;
        private readonly ArchiveCatalog catalog;
        public SeoController(SiteSettings site, ArchiveCatalog catalog) { this.site = site; this.catalog = catalog; }
        [HttpGet("/robots.txt")]
        public IActionResult Robots() => Content(site.AllowIndexing
            ? "User-agent: *\nAllow: /\nSitemap: " + site.Absolute("/sitemap.xml") + "\n"
            : "User-agent: *\nDisallow: /\n", "text/plain; charset=utf-8");

        [HttpGet("/sitemap.xml")]
        [HttpGet("/sitemap-{part:int}.xml")]
        public async Task<IActionResult> Sitemap(int? part = null)
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            if (!site.AllowIndexing) return Content(new XDocument(new XElement(ns + "urlset")).ToString(), "application/xml; charset=utf-8");
            var links = await catalog.GetAsync(HttpContext.RequestAborted);
            var landingPages = links.Where(x => x.Region != null).Select(x => LotteryUrls.RegionPath(x.Region)).Distinct().ToList();
            if (links.Any(x => x.GameCode != null)) landingPages.Add("/vietlott");
            var urls = links.Select(x => (x.Path, x.Modified)).Concat(new[] { "/", "/gioi-thieu", "/nguon-du-lieu", "/support", "/privacy", "/terms", "/lien-he-quang-cao" }.Concat(landingPages)
                .Select(path => (Path: path, Modified: (DateTime?)null))).GroupBy(x => x.Path).Select(g => g.First()).OrderBy(x => x.Path, StringComparer.Ordinal).ToList();
            const int pageSize = 10000;
            int pages = (urls.Count + pageSize - 1) / pageSize;
            if (part.HasValue && (part < 1 || part > pages)) return NotFound();
            XElement root;
            if (!part.HasValue && pages > 1)
                root = new XElement(ns + "sitemapindex", Enumerable.Range(1, pages).Select(n => new XElement(ns + "sitemap", new XElement(ns + "loc", site.Absolute($"/sitemap-{n}.xml")))));
            else
                root = new XElement(ns + "urlset", urls.Skip(((part ?? 1) - 1) * pageSize).Take(pageSize).Select(x =>
                    new XElement(ns + "url", new XElement(ns + "loc", site.Absolute(x.Path)),
                        x.Modified.HasValue ? new XElement(ns + "lastmod", x.Modified.Value.ToString("yyyy-MM-dd")) : null)));
            return Content(new XDocument(root).ToString(), "application/xml; charset=utf-8");
        }
    }
}
