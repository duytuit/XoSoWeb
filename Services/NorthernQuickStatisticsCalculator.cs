using System;
using System.Collections.Generic;
using System.Linq;
using XoSoWeb.Models;

namespace XoSoWeb.Services
{
    public static class NorthernQuickStatisticsCalculator
    {
        public const int HistoryDays = 3650;

        public static NorthernQuickStatisticsViewModel Calculate(DateTime forDate, IEnumerable<NumberRhythmDay> draws)
        {
            var model = new NorthernQuickStatisticsViewModel { ForDate = forDate.Date };
            var start = forDate.Date.AddDays(-HistoryDays);
            var lookup = draws.Where(d => d.HasData && d.Date >= start && d.Date < model.ForDate)
                .GroupBy(d => d.Date.Date).ToDictionary(g => g.Key, g => g.First());
            if (lookup.Count == 0) return model;
            model.DataStart = lookup.Keys.Min();
            model.DataEnd = lookup.Keys.Max();
            model.DrawCount = lookup.Count;
            var days = Enumerable.Range(0, (model.EndDate - model.DataStart.Value).Days + 1)
                .Select(i => lookup.TryGetValue(model.EndDate.AddDays(-i), out var day)
                    ? day : new NumberRhythmDay { Date = model.EndDate.AddDays(-i) }).ToList();
            var month = days.Where(d => d.Date >= model.ForDate.AddDays(-30) && d.HasData).ToList();
            model.MonthDrawCount = month.Count;
            var loto = Enumerable.Range(0, 100).Select(n => Gap(n, days, d => d.Counts[n] > 0)).ToList();
            model.LotoGaps = loto.Where(g => g.Days > 0).OrderByDescending(g => g.Days).ThenBy(g => g.Number).Take(30).ToList();
            model.Leaders = model.LotoGaps.Take(2).ToList();
            model.Frequent = Enumerable.Range(0, 100).Select(n => new QuickFrequency { Number = n, Count = month.Sum(d => d.Counts[n]) })
                .Where(n => n.Count > 0).OrderByDescending(n => n.Count).ThenBy(n => n.Number).Take(30).ToList();
            model.SpecialGaps = Enumerable.Range(0, 100).Select(n => Gap(n, days, d => d.Special == n))
                .Where(g => g.Days > 0).OrderByDescending(g => g.Days).ThenBy(g => g.Number).Take(30).ToList();
            model.ReversePairs = loto.Where(g => g.Number / 10 < g.Number % 10)
                .Select(g => new QuickReversePair { First = g, Second = loto[g.Number % 10 * 10 + g.Number / 10] })
                .Where(p => p.First.Days > 0 && p.Second.Days > 0)
                .OrderByDescending(p => Math.Min(p.First.Days, p.Second.Days))
                .ThenByDescending(p => p.First.Days + p.Second.Days).ThenBy(p => p.First.Number).Take(4).ToList();
            model.Sums = Enumerable.Range(0, 10).Select(n => Gap(n, days, d => (d.Special / 10 + d.Special % 10) % 10 == n))
                .OrderByDescending(g => g.Days).ThenBy(g => g.Number).ToList();
            model.Touches = Enumerable.Range(0, 10).Select(n => Gap(n, days, d => d.Special / 10 == n || d.Special % 10 == n))
                .OrderByDescending(g => g.Days).ThenBy(g => g.Number).ToList();
            return model;
        }

        private static QuickGap Gap(int number, List<NumberRhythmDay> days, Func<NumberRhythmDay, bool> hit)
        {
            var result = new QuickGap { Number = number };
            foreach (var day in days)
            {
                if (!day.HasData) break;
                if (hit(day)) { result.Exact = true; break; }
                result.Days++;
            }
            var run = 0;
            DateTime? runStart = null;
            bool previousHit = false, boundedStart = false;
            void Save(DateTime end, bool boundedEnd)
            {
                var exact = boundedStart && boundedEnd;
                if (run > result.MaxDays || (run == result.MaxDays && exact && !result.MaxExact))
                {
                    result.MaxDays = run; result.MaxStart = runStart; result.MaxEnd = end; result.MaxExact = exact;
                }
            }
            foreach (var day in days.AsEnumerable().Reverse())
            {
                if (!day.HasData || hit(day))
                {
                    if (run > 0) Save(day.Date.AddDays(-1), day.HasData);
                    run = 0; runStart = null; previousHit = day.HasData;
                    continue;
                }
                if (run == 0) { runStart = day.Date; boundedStart = previousHit; }
                run++;
            }
            if (run > 0) Save(days[0].Date, false);
            return result;
        }
    }
}
