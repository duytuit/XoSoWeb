using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace XoSoWeb.Models
{
    public class XoSoPrize
    {
        public static string NormalizeName(string prizeName)
        {
            var original = (prizeName ?? "").Trim();
            var name = new string(original.ToLowerInvariant().Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Replace('đ', 'd');
            name = new string(name.Where(char.IsLetterOrDigit).ToArray());
            if (name.StartsWith("giai")) name = name.Substring(4);
            if (name == "db" || name == "dacbiet" || name == "gdb") return "ĐB";
            if (name.StartsWith("g")) name = name.Substring(1);
            var words = new[] { "", "nhat", "nhi", "ba", "tu", "nam", "sau", "bay", "tam" };
            for (var rank = 1; rank <= 8; rank++)
                if (name == rank.ToString() || name == words[rank]) return rank.ToString();
            return original;
        }

        public static string DisplayName(string prizeName)
        {
            var name = NormalizeName(prizeName);
            return int.TryParse(name, out var rank) && rank >= 1 && rank <= 8 ? "G" + rank : name;
        }

        public static List<XoSoPrize> NorthernDisplayPrizes(IEnumerable<XoSoPrize> prizes)
        {
            var groups = prizes.GroupBy(prize => NormalizeName(prize.PrizeName))
                .ToDictionary(group => group.Key, group => group.SelectMany(prize => prize.Numbers).ToList());
            var names = new[] { "ĐB", "1", "2", "3", "4", "5", "6", "7" };
            return names.Concat(groups.Keys.Where(name => !names.Contains(name)))
                .Select(name => new XoSoPrize
                {
                    PrizeName = name,
                    Numbers = groups.TryGetValue(name, out var numbers) ? numbers : new List<string>()
                }).ToList();
        }

        public static int DisplayOrder(string prizeName)
        {
            var name = NormalizeName(prizeName);
            if (int.TryParse(name, out var rank) && rank >= 1 && rank <= 8) return 8 - rank;
            return name == "ĐB" ? 8 : 9;
        }

        public static bool IsRedPrize(string prizeName, string region)
        {
            var name = (prizeName ?? "").Trim().ToLowerInvariant();
            var normalized = name.Replace("giải", "").Replace("giai", "").Replace(" ", "").Replace(".", "");
            var lastPrize = region == "MB" ? "7" : "8";
            return name.Contains("đb") || name.Contains("db") || name.Contains("đặc biệt")
                || name.Contains("dac biet") || normalized == lastPrize || normalized == "g" + lastPrize
                || (region == "MB" && (normalized == "bảy" || normalized == "bay"))
                || (region != "MB" && (normalized == "tám" || normalized == "tam"));
        }

        public string PrizeName { get; set; }

        public List<string> Numbers { get; set; }
            = new List<string>();
    }
}
