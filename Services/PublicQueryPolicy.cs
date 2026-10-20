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
                "/thong-ke" => new[] { "endDate" },
                "/thong-ke-tan-suat-loto" => new[] { "region", "week" },
                _ => Array.Empty<string>()
            };
            foreach (var item in query)
            {
                if (item.Value.Count != 1 || item.Value.ToString().Length > 256) return false;
                if (item.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("gclid", StringComparison.OrdinalIgnoreCase)
                    || item.Key.Equals("fbclid", StringComparison.OrdinalIgnoreCase)) continue;
                if (!allowed.Contains(item.Key, StringComparer.OrdinalIgnoreCase)) return false;
                var value = item.Value.ToString();
                if (string.IsNullOrEmpty(value)) return false;
                if (item.Key.Equals("day", StringComparison.OrdinalIgnoreCase) && (value.Length != 1 || value[0] < '1' || value[0] > '7')) return false;
                if (item.Key.Equals("offset", StringComparison.OrdinalIgnoreCase)
                    && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset < 0 || offset > 99999 || value != offset.ToString(CultureInfo.InvariantCulture))) return false;
                if ((item.Key.Equals("endDate", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("week", StringComparison.OrdinalIgnoreCase))
                    && !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
            }
            return query.Count <= allowed.Length + 8;
        }
        public static bool Applies(string path) => path == "/" || path.StartsWith("/xo-so-", StringComparison.Ordinal)
            || path == "/vietlott" || path.StartsWith("/vietlott/", StringComparison.Ordinal)
            || path == "/thong-ke" || path == "/thong-ke-tan-suat-loto";
    }
}
