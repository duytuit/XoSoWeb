using System;
using System.Collections.Generic;
using System.Linq;
using XoSoWeb.Models;

namespace XoSoWeb.Services
{
    public sealed class LotteryStation
    {
        public string Id { get; }
        public string Name { get; }
        public string Slug { get; }
        public string Region { get; }
        public string[] Aliases { get; }
        public LotteryStation(string id, string name, string slug, string region, params string[] aliases)
        { Id = id; Name = name; Slug = slug; Region = region; Aliases = aliases.Concat(new[] { id, name, slug }).Distinct().ToArray(); }
    }

    public static class StationCatalog
    {
        public static readonly LotteryStation[] Stations = {
            new LotteryStation("HCM", "TP HCM", "tp-hcm", "MN", "TPHCM", "TP.HCM", "TP. HCM", "Ho Chi Minh", "Thành phố Hồ Chí Minh"),
            new LotteryStation("DL", "Đắk Lắk", "dak-lak", "MT", "Dac Lac", "Đắc Lắc", "Daklak", "DACLAC", "DLK"),
            new LotteryStation("DNO", "Đắk Nông", "dak-nong", "MT", "Dac Nong", "Đắc Nông", "Daknong", "DACNONG", "DNONG")
        };
        private static string Key(string value)
        {
            var slug = LotteryUrls.Slug(value);
            return slug.StartsWith("xo-so-") ? slug.Substring(6) : slug;
        }
        public static LotteryStation Find(string value) => Stations.FirstOrDefault(s => s.Aliases.Any(a => Key(a) == Key(value)));
        public static LotteryStation Resolve(string code, string name) => Find(code) ?? Find(name);
        // SQL Server before JSON support: identity is resolved without SQL JSON functions.
        public static string IdentitySql => "CASE " + string.Join(" ", Stations.Select(s =>
            "WHEN ProvinceCode IN (" + SqlAliases(s) + ") OR ProvinceName IN (" + SqlAliases(s) + ") THEN '" + s.Id + "'")) + " ELSE ProvinceCode END";
        public static string RegionSql => "CASE " + string.Join(" ", Stations.Select(s =>
            "WHEN ProvinceCode IN (" + SqlAliases(s) + ") OR ProvinceName IN (" + SqlAliases(s) + ") THEN '" + s.Region + "'")) + " ELSE Region END";
        private static string SqlAliases(LotteryStation station) => string.Join(",", station.Aliases.SelectMany(a => new[] { a, "Xổ số " + a }).Distinct().Select(a => "N'" + a.Replace("'", "''") + "'"));
        public static void Normalize(XoSoResult result)
        {
            var station = Resolve(result.ProvinceCode, result.ProvinceName);
            if (station == null) return;
            result.ProvinceCode = station.Id; result.ProvinceName = station.Name; result.Region = station.Region;
        }
        // Preserve original rows in storage. Resolve each prize from the preferred source; fill missing prizes from other rows.
        // Legacy storage has no source metadata: completeness, update time, then ID are the explicit fallback.
        public static List<XoSoResult> Merge(IEnumerable<XoSoResult> results)
        {
            var rows = results.ToList();
            foreach (var row in rows) Normalize(row);
            return rows.GroupBy(r => new { Region = LotteryUrls.RegionCode(r.Region),
                    Identity = Resolve(r.ProvinceCode, r.ProvinceName)?.Id ?? (string.IsNullOrWhiteSpace(r.ProvinceName) ? r.ProvinceCode : LotteryUrls.Slug(r.ProvinceName)), Date = r.DrawDate.Date })
                .Select(g => {
                    var ordered = g.OrderByDescending(r => LotteryDataQuality.IsComplete(r.Prizes, g.Key.Region))
                        .ThenByDescending(r => r.SourcePriority)
                        .ThenByDescending(r => r.Prizes.Sum(p => p.Numbers?.Count(n => !string.IsNullOrWhiteSpace(n)) ?? 0))
                        .ThenByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id).ToList();
                    var winner = ordered.First();
                    winner.Prizes = ordered.SelectMany(r => r.Prizes)
                        .GroupBy(p => XoSoPrize.NormalizeName(p.PrizeName))
                        .Select(prizes => prizes.FirstOrDefault(p => LotteryDataQuality.IsCompletePrize(p, g.Key.Region))
                            ?? prizes.Where(p => p.Numbers != null).OrderByDescending(p => p.Numbers.Count(n => !string.IsNullOrWhiteSpace(n))).FirstOrDefault()
                            ?? prizes.First()).ToList();
                    return winner;
                })
                .OrderByDescending(r => r.DrawDate).ThenBy(r => r.ProvinceName).ToList();
        }
    }
}
