using System;
using System.Collections.Generic;
using System.Linq;
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
        [HttpGet("/thong-ke/nhip-so-mien-bac")]
        [HttpGet("/thong-ke/nhip-so-mien-bac/phong-to")]
        public async Task<IActionResult> NumberRhythm(DateTime? endDate = null, int days = 30,
            string target = "loto", string sort = "number", string q = "")
        {
            var selected = endDate?.Date ?? LotteryUrls.Today;
            q = (q ?? "").Trim();
            var pairs = q.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (!ModelState.IsValid || selected > LotteryUrls.Today || selected < new DateTime(1753, 5, 1)
                || days < 1 || days > 120 || (target != "loto" && target != "special")
                || (sort != "number" && sort != "frequency" && sort != "gap") || q.Length > 30
                || (q.Length > 0 && pairs.Length == 0)
                || pairs.Any(p => p.Length != 2 || p.Any(c => c < '0' || c > '9'))) return BadRequest();
            var model = new NumberRhythmViewModel
            {
                EndDate = selected, Days = days, Target = target, Sort = sort,
                Q = string.Join(",", pairs.Distinct().OrderBy(p => p))
            };
            var draws = new List<NumberRhythmDay>();
            var seen = new HashSet<DateTime>();
            var cancellation = HttpContext.RequestAborted;
            await _context.Database.OpenConnectionAsync(cancellation);
            try
            {
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"SELECT DrawDate, Prizes FROM dbo.XoSoResults
                    WHERE DrawDate >= @start AND DrawDate < @end
                    AND Region IN (N'MB', N'Miền bắc', N'Mien Bac', N'Miền bac')
                    ORDER BY DrawDate DESC, UpdatedAt DESC, Id DESC";
                AddParameter(command, "@start", model.StartDate);
                AddParameter(command, "@end", selected.AddDays(1));
                await using var reader = await command.ExecuteReaderAsync(cancellation);
                while (await reader.ReadAsync(cancellation))
                {
                    var drawDate = reader.GetDateTime(0).Date;
                    if (seen.Contains(drawDate) || reader.IsDBNull(1)) continue;
                    List<XoSoPrize> prizes;
                    try { prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(1), JsonOptions); }
                    catch (JsonException) { continue; }
                    var draw = NumberRhythmCalculator.CreateDay(drawDate, prizes);
                    if (draw == null) continue;
                    seen.Add(drawDate);
                    draws.Add(draw);
                }
            }
            finally { await _context.Database.CloseConnectionAsync(); }
            NumberRhythmCalculator.Calculate(model, draws);
            return View(Request.Path.Value.EndsWith("/phong-to", StringComparison.Ordinal) ? "NumberRhythmExpanded" : "NumberRhythm", model);
        }
    }
}
