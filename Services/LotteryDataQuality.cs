using System;
using System.Collections.Generic;
using System.Linq;
using XoSoWeb.Models;

namespace XoSoWeb.Services
{
    // Structural completeness is not official verification. Strings and repeated winning numbers are preserved.
    public static class LotteryDataQuality
    {
        public static bool IsCompletePrize(XoSoPrize prize, string region)
        {
            var name = XoSoPrize.NormalizeName(prize.PrizeName);
            var northern = region == "MB";
            var names = northern ? new[] { "ĐB", "1", "2", "3", "4", "5", "6", "7" }
                : new[] { "ĐB", "1", "2", "3", "4", "5", "6", "7", "8" };
            var counts = northern ? new[] { 1, 1, 2, 6, 4, 6, 3, 4 } : new[] { 1, 1, 1, 2, 7, 1, 3, 1, 1 };
            var widths = northern ? new[] { 5, 5, 5, 5, 4, 4, 3, 2 } : new[] { 6, 5, 5, 5, 5, 4, 4, 3, 2 };
            var index = Array.IndexOf(names, name);
            return index >= 0 && prize.Numbers != null && prize.Numbers.Count == counts[index]
                && prize.Numbers.All(n => n != null && n.Length == widths[index] && n.All(c => c >= '0' && c <= '9'));
        }
        public static bool IsComplete(IEnumerable<XoSoPrize> prizes, string region)
        {
            if (region != "MB" && region != "MT" && region != "MN") return false;
            var grouped = prizes.Where(p => p != null).GroupBy(p => XoSoPrize.NormalizeName(p.PrizeName))
                .Select(g => new XoSoPrize { PrizeName = g.Key, Numbers = g.SelectMany(p => p.Numbers ?? new()).ToList() }).ToList();
            return grouped.Count == (region == "MB" ? 8 : 9) && grouped.All(p => IsCompletePrize(p, region));
        }
    }
}
