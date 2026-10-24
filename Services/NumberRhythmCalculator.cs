using System;
using System.Collections.Generic;
using System.Linq;
using XoSoWeb.Models;

namespace XoSoWeb.Services
{
    public static class NumberRhythmCalculator
    {
        public static NumberRhythmDay CreateDay(DateTime date, List<XoSoPrize> prizes)
        {
            if (prizes == null || prizes.Any(p => p == null || p.Numbers == null)
                || SoiCauCalculator.CreateDraw(date, prizes) == null) return null;
            var day = new NumberRhythmDay { Date = date.Date, HasData = true };
            foreach (var prize in prizes)
            {
                var isSpecial = XoSoPrize.NormalizeName(prize.PrizeName) == "ĐB";
                foreach (var value in prize.Numbers)
                {
                    // CreateDraw validated the eight northern prize groups. Ignore extra groups.
                    var name = XoSoPrize.NormalizeName(prize.PrizeName);
                    if (name != "ĐB" && name != "1" && name != "2" && name != "3"
                        && name != "4" && name != "5" && name != "6" && name != "7") continue;
                    var number = int.Parse(value.Substring(value.Length - 2), System.Globalization.CultureInfo.InvariantCulture);
                    day.Counts[number]++;
                    if (isSpecial) day.Special = number;
                }
            }
            return day;
        }

        public static void Calculate(NumberRhythmViewModel model, IEnumerable<NumberRhythmDay> draws)
        {
            var lookup = draws.Where(d => d.HasData && d.Date >= model.StartDate && d.Date <= model.EndDate)
                .GroupBy(d => d.Date.Date).ToDictionary(g => g.Key, g => g.First());
            model.Rows = Enumerable.Range(0, model.Days).Select(i =>
                lookup.TryGetValue(model.EndDate.AddDays(-i), out var draw)
                    ? draw : new NumberRhythmDay { Date = model.EndDate.AddDays(-i) }).ToList();
            model.DrawCount = model.Rows.Count(d => d.HasData);
            var numbers = model.Q.Length == 0 ? Enumerable.Range(0, 100)
                : model.Q.Split(',').Select(int.Parse).Distinct();
            model.Columns = new List<NumberRhythmColumn>();
            foreach (var number in numbers)
            {
                var column = new NumberRhythmColumn { Number = number };
                bool countingGap = true;
                foreach (var day in model.Rows)
                {
                    if (!day.HasData) { countingGap = false; continue; }
                    var count = Count(model, day, number);
                    column.Total += count;
                    if (count > 0)
                    {
                        column.HitDays++;
                        column.LastSeen ??= day.Date;
                    }
                    if (!countingGap) continue;
                    if (count == 0) column.Gap++;
                    else { column.GapExact = true; countingGap = false; }
                }
                DateTime? previousHit = null;
                foreach (var day in model.Rows.AsEnumerable().Reverse())
                {
                    if (!day.HasData) { previousHit = null; continue; }
                    if (Count(model, day, number) == 0) continue;
                    if (previousHit.HasValue) column.Cycles.Add(new NumberRhythmCycle { From = previousHit.Value, To = day.Date });
                    previousHit = day.Date;
                }
                column.Cycles.Reverse();
                model.Columns.Add(column);
            }
            model.Columns = model.Sort switch
            {
                "frequency" => model.Columns.OrderByDescending(c => c.Total).ThenBy(c => c.Number).ToList(),
                "gap" => model.Columns.OrderByDescending(c => c.Gap).ThenBy(c => c.Number).ToList(),
                _ => model.Columns.OrderBy(c => c.Number).ToList()
            };
        }

        public static int Count(NumberRhythmViewModel model, NumberRhythmDay day, int number) =>
            model.Target == "special" ? (day.Special == number ? 1 : 0) : day.Counts[number];
    }
}
