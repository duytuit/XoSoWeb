using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XoSoWeb.Models;
using XoSoWeb.Services;
namespace XoSoWeb.Controllers
{
    public partial class HomeController
    {
        [HttpGet("/thong-ke/soi-cau")]
        public async Task<IActionResult> SoiCau(DateTime? date = null, int length = 5, string target = "loto", bool reverse = true, string q = "")
        {
            q = (q ?? "").Trim();
            var selected = date?.Date ?? LotteryUrls.Today;
            if (!ModelState.IsValid || selected > LotteryUrls.Today || selected < new DateTime(1753, 2, 1)
                || length < 1 || length > 10 || (target != "loto" && target != "special")
                || (q.Length > 0 && (q.Length != 2 || q[0] < '0' || q[0] > '9' || q[1] < '0' || q[1] > '9'))) return BadRequest();
            var model = new SoiCauViewModel { Date = selected, Length = length, Target = target, Reverse = reverse, Q = q };
            var cancellation = HttpContext.RequestAborted;
            await _context.Database.OpenConnectionAsync(cancellation);
            try
            {
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"SELECT DrawDate, Prizes FROM dbo.XoSoResults
                    WHERE DrawDate >= @start AND DrawDate < @end
                    AND Region IN (N'MB', N'Miền bắc', N'Mien Bac', N'Miền bac') ORDER BY DrawDate DESC, Id DESC";
                AddParameter(command, "@start", selected.AddDays(-31));
                AddParameter(command, "@end", selected);
                await using var reader = await command.ExecuteReaderAsync(cancellation);
                var seen = new HashSet<DateTime>();
                while (await reader.ReadAsync(cancellation))
                {
                    var drawDate = reader.GetDateTime(0).Date;
                    if (seen.Contains(drawDate)) continue;
                    List<XoSoPrize> prizes;
                    try { prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(1), JsonOptions); }
                    catch (JsonException) { continue; }
                    if (prizes == null || prizes.Exists(p => p == null || p.Numbers == null)) continue;
                    var draw = SoiCauCalculator.CreateDraw(drawDate, prizes);
                    if (draw == null) continue;
                    seen.Add(drawDate);
                    model.Draws.Add(draw);
                }
            }
            finally { await _context.Database.CloseConnectionAsync(); }
            SoiCauCalculator.Calculate(model);
            return View(model);
        }
    }
}
