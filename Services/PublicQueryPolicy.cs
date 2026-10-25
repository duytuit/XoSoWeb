using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace XoSoWeb.Services
{
    public static class PublicQueryPolicy
    {
        public static bool IsValid(string path, IQueryCollection query)
        {
            string[] allowed = path switch {
                "/xo-so-mien-bac" or "/xo-so-mien-trung" or "/xo-so-mien-nam" => new[] { "day" },
                "/vietlott" => new[] { "gameCode", "offset" },
                "/thong-ke/nhip-so-mien-bac" or "/thong-ke/nhip-so-mien-bac/phong-to" => new[] { "endDate", "days", "target", "sort", "q" },
                "/thong-ke/soi-cau" => new[] { "date", "length", "target", "reverse", "q" },
                "/thong-ke" => new[] { "endDate" },
                "/thong-ke-tan-suat-loto" => new[] { "region", "week" },
                _ when path.StartsWith("/thong-ke/vietlott/", StringComparison.Ordinal) => new[] { "periods", "endDraw", "prize", "sort", "q", "page" },
                "/thong-ke/vietlott" => Array.Empty<string>(),
                "/lich-quay/vietlott" => new[] { "game", "weekday" },
                _ when path.StartsWith("/lich-quay/", StringComparison.Ordinal) => new[] { "station", "weekday" },
                "/tin-tuc" or "/tin-tuc/vietlott" or "/tin-tuc/trung-thuong-vietlott" or "/tin-tuc/huong-dan" => new[] { "page" },
                _ => Array.Empty<string>()
            };
            foreach (var item in query)
            {
                if (item.Value.Count != 1 || item.Value.ToString().Length > 256) return false;
                if (item.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("gclid", StringComparison.OrdinalIgnoreCase)
                    || item.Key.Equals("fbclid", StringComparison.OrdinalIgnoreCase)) continue;
                if (!allowed.Contains(item.Key, StringComparer.OrdinalIgnoreCase)) return false;
                var value = item.Value.ToString();
                if (string.IsNullOrEmpty(value)
                    && !item.Key.Equals("endDraw", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("prize", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("q", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("station", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("weekday", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("game", StringComparison.OrdinalIgnoreCase)) return false;
                if (item.Key.Equals("day", StringComparison.OrdinalIgnoreCase) && (value.Length != 1 || value[0] < '1' || value[0] > '7')) return false;
                if (item.Key.Equals("offset", StringComparison.OrdinalIgnoreCase)
                    && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 0 || offset > 99999 || value != offset.ToString(CultureInfo.InvariantCulture))) return false;
                if (item.Key.Equals("periods", StringComparison.OrdinalIgnoreCase) && value != "20" && value != "50" && value != "100" && value != "200") return false;
                if (item.Key.Equals("page", StringComparison.OrdinalIgnoreCase)
                    && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page) || page < 1 || page > 100000 || value != page.ToString(CultureInfo.InvariantCulture))) return false;
                if (item.Key.Equals("sort", StringComparison.OrdinalIgnoreCase) && value != "number" && value != "frequency" && value != "gap") return false;
                if (item.Key.Equals("endDraw", StringComparison.OrdinalIgnoreCase) && value.Length > 0 && !LotteryUrls.ValidDrawNo(value)) return false;
                if (item.Key.Equals("prize", StringComparison.OrdinalIgnoreCase) && value.Length > 100) return false;
                if (item.Key.Equals("q", StringComparison.OrdinalIgnoreCase) && value.Length > 30) return false;
                if (item.Key.Equals("weekday", StringComparison.OrdinalIgnoreCase)
                    && value.Length > 0
                    && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var weekday)
                        || weekday < 1 || weekday > 7 || value != weekday.ToString(CultureInfo.InvariantCulture))) return false;
                if (item.Key.Equals("game", StringComparison.OrdinalIgnoreCase)
                    && value.Length > 0
                    && !new[] { "mega645", "power655", "lotto535", "keno", "max3d", "max3d-plus", "max3d-pro", "bingo18" }.Contains(value, StringComparer.Ordinal)) return false;
                if (item.Key.Equals("station", StringComparison.OrdinalIgnoreCase) && value.Length > 100) return false;
                if ((item.Key.Equals("date", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("endDate", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("week", StringComparison.OrdinalIgnoreCase))
                    && !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
            }
            return query.Count <= allowed.Length + 8;
        }
        public static bool Applies(string path) => path == "/" || path.StartsWith("/xo-so-", StringComparison.Ordinal)
            || path == "/vietlott" || path.StartsWith("/vietlott/", StringComparison.Ordinal)
            || path == "/thong-ke/nhip-so-mien-bac" || path == "/thong-ke/nhip-so-mien-bac/phong-to" || path == "/thong-ke/soi-cau" || path == "/thong-ke" || path == "/thong-ke-tan-suat-loto"
            || path == "/thong-ke/vietlott" || path.StartsWith("/thong-ke/vietlott/", StringComparison.Ordinal)
            || path == "/lich-quay" || path.StartsWith("/lich-quay/", StringComparison.Ordinal)
            || path == "/co-cau-giai-thuong" || path.StartsWith("/co-cau-giai-thuong/", StringComparison.Ordinal)
            || path == "/tin-tuc" || path.StartsWith("/tin-tuc/", StringComparison.Ordinal);
    }
}
