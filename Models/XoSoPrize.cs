using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class XoSoPrize
    {
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