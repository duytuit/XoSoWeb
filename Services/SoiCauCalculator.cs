using System;
using System.Linq;
using System.Collections.Generic;
using XoSoWeb.Models;
namespace XoSoWeb.Services
{
    public static class SoiCauCalculator
    {
        // Only complete northern draws have stable digit positions (107 digits).
        public static SoiCauDraw CreateDraw(DateTime date, IEnumerable<XoSoPrize> prizes)
        {
            var ordered = XoSoPrize.NorthernDisplayPrizes(prizes).Take(8).ToList();
            var counts = new[] { 1, 1, 2, 6, 4, 6, 3, 4 };
            var widths = new[] { 5, 5, 5, 5, 4, 4, 3, 2 };
            var draw = new SoiCauDraw { Date = date.Date, Digits = "" };
            for (var p = 0; p < 8; p++)
            {
                if (ordered[p].Numbers.Count != counts[p]) return null;
                for (var n = 0; n < counts[p]; n++)
                {
                    var number = ordered[p].Numbers[n];
                    if (number == null || number.Length != widths[p] || number.Any(c => c < '0' || c > '9')) return null;
                    draw.Digits += number;
                    draw.Loto.Add(number.Substring(number.Length - 2));
                    for (var d = 0; d < number.Length; d++)
                        draw.Labels.Add($"{XoSoPrize.DisplayName(ordered[p].PrizeName)} · số {n + 1} · chữ số {d + 1}");
                    if (p == 0) draw.Special = number.Substring(number.Length - 2);
                }
            }
            return draw;
        }
        public static void Calculate(SoiCauViewModel model)
        {
            model.Patterns.Clear();
            var draws = model.Draws.OrderByDescending(d => d.Date).ToList();
            model.Draws = draws;
            if (draws.Count < 2 || draws[0].Date != model.Date.AddDays(-1)) return;
            for (var a = 0; a < 107; a++)
            for (var b = model.Reverse ? a + 1 : 0; b < 107; b++)
            {
                if (a == b) continue;
                var number = string.Concat(draws[0].Digits[a], draws[0].Digits[b]);
                var reversed = new string(number.Reverse().ToArray());
                if (model.Q.Length > 0 && number != model.Q && (!model.Reverse || reversed != model.Q)) continue;
                var pattern = new SoiCauPattern { First = a, Second = b, Number = number };
                for (var i = 1; i < draws.Count; i++)
                {
                    var source = draws[i]; var result = draws[i - 1];
                    if (source.Date.AddDays(1) != result.Date) break;
                    var candidate = string.Concat(source.Digits[a], source.Digits[b]);
                    var reverse = new string(candidate.Reverse().ToArray());
                    var hits = model.Target == "special" ? new HashSet<string> { result.Special } : result.Loto;
                    var matched = hits.Contains(candidate) ? candidate : model.Reverse && hits.Contains(reverse) ? reverse : null;
                    if (matched == null) break;
                    pattern.Steps.Add(new SoiCauStep { SourceDate = source.Date, ResultDate = result.Date, Number = candidate, Matched = matched });
                }
                if (pattern.Steps.Count >= model.Length) model.Patterns.Add(pattern);
            }
            model.Patterns = model.Patterns.OrderByDescending(p => p.Steps.Count).ThenBy(p => p.Number).ThenBy(p => p.First).ThenBy(p => p.Second).ToList();
        }
    }
}
