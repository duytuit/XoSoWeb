using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;

namespace XoSoWeb.Controllers
{
    public partial class HomeController
    {
        [HttpGet("/thong-ke/vietlott")]
        public async Task<IActionResult> VietlottStatisticsIndex([FromServices] VietlottStatisticsService statistics)
        {
            var model = await statistics.GetIndexAsync(HttpContext.RequestAborted);
            ViewData["Title"] = "Thống kê Vietlott";
            ViewData["Description"] = "Các công cụ thống kê kết quả Vietlott theo từng trò chơi, tính từ những kỳ hoàn chỉnh có trong dữ liệu.";
            ViewData["ParentPath"] = "/";
            ViewData["ParentLabel"] = "Trang chủ";
            if (model.Games.Count == 0) ViewData["NoIndex"] = true;
            return View("VietlottStatisticsIndex", model);
        }

        [HttpGet("/thong-ke/vietlott/{gameSlug}")]
        public async Task<IActionResult> VietlottStatisticsGame(
            string gameSlug, [FromServices] VietlottStatisticsService statistics)
        {
            if (string.IsNullOrWhiteSpace(gameSlug)) return NotFound();
            var model = await statistics.GetGameAsync(gameSlug, HttpContext.RequestAborted);
            if (model == null) return NotFound();
            ViewData["Title"] = "Thống kê " + model.Name;
            ViewData["Description"] = "Công cụ thống kê " + model.Name + " từ các kỳ quay hoàn chỉnh hiện có.";
            ViewData["ParentPath"] = "/thong-ke/vietlott";
            ViewData["ParentLabel"] = "Thống kê Vietlott";
            return View("VietlottStatisticsGame", model);
        }

        [HttpGet("/thong-ke/vietlott/{gameSlug}/{toolSlug}")]
        public async Task<IActionResult> VietlottStatisticsTool(
            string gameSlug, string toolSlug,
            [FromQuery] int periods = 50,
            [FromQuery] string endDraw = null,
            [FromQuery] string prize = null,
            [FromQuery] string sort = "number",
            [FromQuery(Name = "q")] string search = null,
            [FromQuery] int page = 1,
            [FromServices] VietlottStatisticsService statistics = null)
        {
            if (!ModelState.IsValid) return BadRequest();
            var model = await statistics.GetPageAsync(gameSlug, toolSlug, periods, endDraw, prize, sort, search, page, HttpContext.RequestAborted);
            if (model == null) return NotFound();
            ViewData["Title"] = model.ToolName + " " + model.GameName;
            ViewData["Description"] = model.Description + " Tính trên " + model.UsedPeriods + " kỳ hoàn chỉnh trong dữ liệu.";
            ViewData["ParentPath"] = "/thong-ke/vietlott/" + model.GameSlug;
            ViewData["ParentLabel"] = "Thống kê " + model.GameName;
            if (model.UsedPeriods == 0) ViewData["NoIndex"] = true;
            return View("VietlottStatisticsTool", model);
        }
    }
}
