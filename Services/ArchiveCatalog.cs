using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    // Cache navigation metadata only. Result tables always read fresh data, including during draws.
    public sealed class ArchiveCatalog
    {
        private readonly IServiceScopeFactory scopes;
        private readonly IMemoryCache cache;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        public ArchiveCatalog(IServiceScopeFactory scopes, IMemoryCache cache) { this.scopes = scopes; this.cache = cache; }
        public void Invalidate() { cache.Remove("archive-catalog"); PublicContentVersion.Invalidate(); }
        public async Task<List<ArchiveLink>> GetAsync(CancellationToken cancellation = default)
        {
            if (cache.TryGetValue("archive-catalog", out List<ArchiveLink> found)) return found;
            await gate.WaitAsync(cancellation);
            try
            {
                if (cache.TryGetValue("archive-catalog", out found)) return found;
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<XoSoDBContext>();
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync(cancellation);
                var entries = new List<ArchiveLink>();
                await using (var command = connection.CreateCommand())
                {
                    // Validate JSON in .NET: the deployed SQL Server predates SQL JSON functions.
                    command.CommandText = @"SELECT Region, ProvinceCode, ProvinceName, CAST(DrawDate AS date), UpdatedAt, Prizes
                        FROM dbo.XoSoResults WHERE DrawDate < @tomorrow
                        AND NULLIF(LTRIM(RTRIM(Prizes)), '') IS NOT NULL";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "@tomorrow";
                    parameter.Value = LotteryUrls.Today.AddDays(1); command.Parameters.Add(parameter);
                    await using var reader = await command.ExecuteReaderAsync(cancellation);
                    while (await reader.ReadAsync(cancellation))
                    {
                        if (!HasResultNumbers(reader.GetString(5))) continue;
                        var region = LotteryUrls.RegionCode(reader.GetString(0));
                        if (region == null) continue;
                        var station = StationCatalog.Resolve(reader.GetString(1), reader.GetString(2));
                        region = station?.Region ?? region;
                        var name = station?.Name ?? reader.GetString(2); var date = reader.GetDateTime(3);
                        if (date.Date < new DateTime(1753, 1, 1) || date.Date > LotteryUrls.Today || string.IsNullOrWhiteSpace(name)) continue;
                        var province = LotteryUrls.ProvincePath(name);
                        entries.Add(new ArchiveLink { Region = region, ProvinceCode = station?.Id ?? reader.GetString(1), ProvinceName = name,
                            Path = province + "/" + date.ToString("dd-MM-yyyy"), Label = name + " · " + date.ToString("dd/MM/yyyy"),
                            Date = date, Modified = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                            IsIndexable = LotteryDataQuality.IsComplete(ReadPrizes(reader.GetString(5)), region) });
                    }
                }
                var regions = entries.GroupBy(x => new { x.Region, x.Date }).Select(g => new ArchiveLink {
                    Path = LotteryUrls.RegionPath(g.Key.Region) + "/" + g.Key.Date.Value.ToString("dd-MM-yyyy"),
                    Label = "Xổ số " + (g.Key.Region == "MB" ? "Miền Bắc" : g.Key.Region == "MT" ? "Miền Trung" : "Miền Nam") + " · " + g.Key.Date.Value.ToString("dd/MM/yyyy"),
                    Region = g.Key.Region, Date = g.Key.Date, Modified = g.Max(x => x.Modified), IsIndexable = g.Any(x => x.IsIndexable) }).ToList();
                // Northern aggregate names already have regional routes; do not create duplicate province pages.
                var regionalPaths = new HashSet<string>(regions.Select(x => x.Path), StringComparer.Ordinal);
                entries = entries.Where(x => !regionalPaths.Contains(x.Path)).ToList();
                entries.AddRange(regions);
                entries.AddRange(entries.Where(x => x.ProvinceCode != null).GroupBy(x => x.Path.Substring(0, x.Path.LastIndexOf('/')))
                    .Select(g => new ArchiveLink { Path = g.Key, Label = g.First().ProvinceName, Region = g.First().Region,
                        ProvinceCode = g.First().ProvinceCode, ProvinceName = g.First().ProvinceName, Modified = g.Max(x => x.Modified), IsIndexable = g.Any(x => x.IsIndexable) }).ToList());
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT v.GameCode, v.DrawNo, v.DrawDate, v.Numbers,
                        CASE WHEN EXISTS (SELECT 1 FROM dbo.VietlottPrizes p WHERE p.ResultId = v.Id AND NULLIF(LTRIM(RTRIM(p.Numbers)), '') IS NOT NULL AND p.Numbers LIKE '%[0-9]%') THEN 1 ELSE 0 END AS HasPrizes FROM dbo.VietlottResults v
                        WHERE v.DrawDate < @tomorrow AND (NULLIF(LTRIM(RTRIM(v.Numbers)), '') IS NOT NULL
                        OR EXISTS (SELECT 1 FROM dbo.VietlottPrizes p WHERE p.ResultId = v.Id AND NULLIF(LTRIM(RTRIM(p.Numbers)), '') IS NOT NULL AND p.Numbers LIKE '%[0-9]%'))";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "@tomorrow";
                    parameter.Value = LotteryUrls.Today.AddDays(1); command.Parameters.Add(parameter);
                    await using var reader = await command.ExecuteReaderAsync(cancellation);
                    while (await reader.ReadAsync(cancellation))
                    {
                        var game = reader.GetString(0); var draw = reader.GetString(1);
                        if (!SupportedGames.Contains(game) || !LotteryUrls.ValidDrawNo(draw) || reader.GetDateTime(2).Date > LotteryUrls.Today
                            || reader.GetDateTime(2).Date < new DateTime(1753, 1, 1)
                            || !HasVietlottContent(game, (reader.IsDBNull(3) ? "" : reader.GetString(3)).Split(new[] { ' ', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries), reader.GetInt32(4) == 1)) continue;
                        entries.Add(new ArchiveLink { GameCode = game, DrawNo = draw, Date = reader.GetDateTime(2),
                            Path = LotteryUrls.DrawPath(game, draw), Label = LotteryUrls.GameName(game) + " · kỳ " + draw + " · " + reader.GetDateTime(2).ToString("dd/MM/yyyy") });
                    }
                }
                entries.AddRange(entries.Where(x => x.GameCode != null).Select(x => x.GameCode).Distinct()
                    .Select(game => new ArchiveLink { GameCode = game, Path = LotteryUrls.GamePath(game), Label = LotteryUrls.GameName(game) }).ToList());
                entries = entries.GroupBy(x => x.Path, StringComparer.Ordinal).Select(g => g.OrderByDescending(x => x.IsIndexable).ThenByDescending(x => x.Modified).First()).ToList();
                cache.Set("archive-catalog", entries, TimeSpan.FromMinutes(2));
                return entries;
            }
            finally { gate.Release(); }
        }
        public static List<XoSoPrize> ReadPrizes(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<XoSoPrize>();
            try
            {
                var prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return prizes != null && prizes.All(x => x != null && x.PrizeName != null && x.Numbers != null)
                    ? prizes : new List<XoSoPrize>();
            }
            catch (JsonException) { return new List<XoSoPrize>(); }
        }
        public static bool HasResultNumbers(string json) => ReadPrizes(json).Any(p => p.Numbers.Any(n => !string.IsNullOrWhiteSpace(n)));
        public static bool HasVietlottContent(string game, IEnumerable<string> values, bool hasPrizes)
        {
            var numbers = values.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (game == "KENO") return numbers.Count == 20 && numbers.All(n => int.TryParse(n, out var value) && value >= 1 && value <= 80)
                && numbers.Select(int.Parse).Distinct().Count() == 20;
            if (game == "BINGO18") return numbers.Count == 3 && numbers.All(n => int.TryParse(n, out var value) && value >= 1 && value <= 6);
            if (game == "MEGA645" || game == "POWER655" || game == "LOTTO535")
            {
                var count = game == "LOTTO535" ? 5 : 6;
                var maximum = game == "MEGA645" ? 45 : game == "POWER655" ? 55 : 35;
                return numbers.Count == count && numbers.All(n => n.All(c => c >= '0' && c <= '9') && int.TryParse(n, out var value) && value >= 1 && value <= maximum)
                    && numbers.Select(int.Parse).Distinct().Count() == count;
            }
            return (numbers.Count > 0 && numbers.All(n => n.All(c => c >= '0' && c <= '9'))) || hasPrizes;
        }
        public static readonly string[] SupportedGames = { "BINGO18", "KENO", "LOTTO535", "MAX3D", "MAX3D_PLUS", "MAX3D_PRO", "MEGA645", "POWER655" };
    }
}
