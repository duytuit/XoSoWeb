using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;

namespace XoSoWeb.Controllers
{
    public partial class HomeController
    {
        [HttpGet("/xo-so-{slug}/{date}")]
        [HttpGet("/xo-so-{slug}")]
        public async Task<IActionResult> LotteryArchive(string slug, string date, [FromServices] ArchiveCatalog catalog)
        {
            DateTime parsed = default;
            if (date != null && !LotteryUrls.TryDate(date, out parsed)) return NotFound();
            var path = "/xo-so-" + slug + (date == null ? "" : "/" + date);
            var entries = await catalog.GetAsync(HttpContext.RequestAborted);
            var entry = entries.FirstOrDefault(x => x.Path == path && x.GameCode == null);
            // A new draw can arrive before the navigation cache expires. The exact SQL query
            // remains authoritative; do not hide that result behind a cached URL list.
            if (entry == null && date != null)
            {
                var region = slug == "mien-bac" ? "MB" : slug == "mien-trung" ? "MT" : slug == "mien-nam" ? "MN" : null;
                var province = entries.FirstOrDefault(x => x.Path == "/xo-so-" + slug && x.ProvinceCode != null && !x.Date.HasValue);
                if (region != null || province != null)
                    entry = new ArchiveLink { Path = path, Region = region ?? province.Region, ProvinceCode = province?.ProvinceCode,
                        ProvinceName = province?.ProvinceName, Date = parsed,
                        Label = (province?.ProvinceName ?? (region == "MB" ? "Xổ số Miền Bắc" : region == "MT" ? "Xổ số Miền Trung" : "Xổ số Miền Nam")) + " · " + parsed.ToString("dd/MM/yyyy") };
            }
            if (entry == null) return NotFound();
            var regionName = entry.Region == "MB" ? "Miền Bắc" : entry.Region == "MT" ? "Miền Trung" : "Miền Nam";
            var model = new ArchivePage { Title = date == null ? "Xổ số " + entry.Label : entry.Label,
                ParentPath = date != null && entry.ProvinceCode != null ? LotteryUrls.ProvincePath(entry.ProvinceName) : LotteryUrls.RegionPath(entry.Region),
                ParentLabel = date != null && entry.ProvinceCode != null ? entry.ProvinceName : "Xổ số " + regionName,
                Regional = await GetRegionResultsAsync(entry.Region, regionName, null, date == null ? 7 : 1, date == null ? null : parsed, entry.ProvinceName) };
            if (model.Regional.Results.Count == 0) return NotFound();
            var siblings = entries.Where(x => x.Region == entry.Region && x.ProvinceName == entry.ProvinceName && x.Date.HasValue)
                .OrderByDescending(x => x.Date).ToList();
            model.Links = siblings.Take(30).ToList();
            if (date != null)
            {
                model.Previous = siblings.FirstOrDefault(x => x.Date < parsed);
                model.Next = siblings.Where(x => x.Date > parsed).OrderBy(x => x.Date).FirstOrDefault();
            }
            ViewData["Title"] = model.Title;
            ViewData["Description"] = "Tra cứu bảng giải " + model.Title + ", ngày quay và các kỳ có dữ liệu liên quan.";
            ViewData["ParentPath"] = model.ParentPath;
            ViewData["ParentLabel"] = model.ParentLabel;
            return View("Archive", model);
        }

        [HttpGet("/vietlott/{game}")]
        [HttpGet("/vietlott/{game}/ky/{drawNo}")]
        public async Task<IActionResult> VietlottArchive(string game, string drawNo, [FromServices] ArchiveCatalog catalog)
        {
            var code = ArchiveCatalog.SupportedGames.FirstOrDefault(x => LotteryUrls.Slug(x) == game);
            if (code == null) return NotFound();
            var entries = await catalog.GetAsync(HttpContext.RequestAborted);
            var path = drawNo == null ? LotteryUrls.GamePath(code) : LotteryUrls.DrawPath(code, drawNo);
            var entry = entries.FirstOrDefault(x => x.Path == path);
            if (entry == null && drawNo != null)
                entry = new ArchiveLink { Path = path, GameCode = code, DrawNo = drawNo, Label = LotteryUrls.GameName(code) + " · kỳ " + drawNo };
            if (entry == null) return NotFound();
            var page = await GetVietlottResultsAsync(code, 0, drawNo);
            if (page.Results.Count == 0) return NotFound();
            var siblings = entries.Where(x => x.GameCode == code && x.DrawNo != null).OrderByDescending(x => x.Date).ThenByDescending(x => x.DrawNo, StringComparer.Ordinal).ToList();
            var index = siblings.FindIndex(x => x.Path == path);
            var model = new ArchivePage { Title = entry.Label, ParentPath = drawNo == null ? "/vietlott" : LotteryUrls.GamePath(code),
                ParentLabel = drawNo == null ? "Vietlott" : LotteryUrls.GameName(code), Vietlott = page, Links = siblings.Take(30).ToList(),
                Previous = index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null,
                Next = index > 0 ? siblings[index - 1] : null };
            ViewData["Title"] = "Kết quả " + model.Title;
            ViewData["Description"] = "Kết quả " + model.Title + ", mã kỳ quay, ngày quay và bảng giải được lưu trong hệ thống.";
            ViewData["ParentPath"] = model.ParentPath;
            ViewData["ParentLabel"] = model.ParentLabel;
            return View("Archive", model);
        }

        [HttpGet("/gioi-thieu")]
        public IActionResult Introduction() => View("Information", "introduction");
        [HttpGet("/nguon-du-lieu")]
        public IActionResult DataSources() => View("Information", "sources");
    }
}
