using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;

namespace XoSoWeb.Controllers
{
    public sealed class LotteryReferenceController : Controller
    {
        [HttpGet("/lich-quay")]
        public IActionResult ScheduleIndex()
        {
            var model = LotteryReferenceCatalog.BuildIndex(true);
            SetMetadata(model, true);
            return View("~/Views/Home/LotteryReference.cshtml", model);
        }

        [HttpGet("/lich-quay/{region}")]
        public IActionResult Schedule(
            string region, [FromQuery] int? weekday, [FromQuery] string station, [FromQuery] string game)
        {
            if (!ModelState.IsValid || (station != null && station.Length > 100)) return BadRequest();
            station = string.IsNullOrWhiteSpace(station) ? null : station.Trim();
            game = string.IsNullOrWhiteSpace(game) ? null : game.Trim();
            if (!LotteryReferenceCatalog.TryBuildSchedule(region, weekday, station, game, out var model))
                return NotFound();
            SetMetadata(model, true);
            return View("~/Views/Home/LotteryReference.cshtml", model);
        }

        [HttpGet("/co-cau-giai-thuong")]
        public IActionResult PrizeIndex()
        {
            var model = LotteryReferenceCatalog.BuildIndex(false);
            SetMetadata(model, false);
            return View("~/Views/Home/LotteryReference.cshtml", model);
        }

        [HttpGet("/co-cau-giai-thuong/{region}")]
        public IActionResult RegionalPrizeStructure(string region)
        {
            if (!LotteryReferenceCatalog.TryBuildPrizeStructure(region, null, out var model))
                return NotFound();
            SetMetadata(model, false);
            return View("~/Views/Home/LotteryReference.cshtml", model);
        }

        [HttpGet("/co-cau-giai-thuong/vietlott/{game}")]
        public IActionResult VietlottPrizeStructure(string game)
        {
            if (!LotteryReferenceCatalog.TryBuildPrizeStructure("vietlott", game, out var model))
                return NotFound();
            SetMetadata(model, false);
            return View("~/Views/Home/LotteryReference.cshtml", model);
        }

        private void SetMetadata(LotteryReferencePageViewModel model, bool schedule)
        {
            ViewData["Title"] = model.Title;
            ViewData["Description"] = model.Description;
            ViewData["ParentPath"] = model.PageKind.EndsWith("-index", StringComparison.Ordinal) ? "/" : model.ParentPath;
            ViewData["ParentLabel"] = model.ParentLabel;
            ViewData["NoIndex"] = true;
        }
    }
}
