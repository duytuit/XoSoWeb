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
        public void Invalidate() => cache.Remove("archive-catalog");
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
                        var name = reader.GetString(2); var date = reader.GetDateTime(3);
                        var province = LotteryUrls.ProvincePath(name);
                        entries.Add(new ArchiveLink { Region = region, ProvinceCode = reader.GetString(1), ProvinceName = name,
                            Path = province + "/" + date.ToString("dd-MM-yyyy"), Label = name + " · " + date.ToString("dd/MM/yyyy"),
                            Date = date, Modified = reader.IsDBNull(4) ? null : reader.GetDateTime(4) });
                    }
                }
                var regions = entries.GroupBy(x => new { x.Region, x.Date }).Select(g => new ArchiveLink {
                    Path = LotteryUrls.RegionPath(g.Key.Region) + "/" + g.Key.Date.Value.ToString("dd-MM-yyyy"),
                    Label = "Xổ số " + (g.Key.Region == "MB" ? "Miền Bắc" : g.Key.Region == "MT" ? "Miền Trung" : "Miền Nam") + " · " + g.Key.Date.Value.ToString("dd/MM/yyyy"),
                    Region = g.Key.Region, Date = g.Key.Date, Modified = g.Max(x => x.Modified) }).ToList();
                // Northern aggregate names already have regional routes; do not create duplicate province pages.
                var regionalPaths = new HashSet<string>(regions.Select(x => x.Path), StringComparer.Ordinal);
                entries = entries.Where(x => !regionalPaths.Contains(x.Path)).ToList();
                entries.AddRange(regions);
                entries.AddRange(entries.Where(x => x.ProvinceCode != null).GroupBy(x => x.Path.Substring(0, x.Path.LastIndexOf('/')))
                    .Select(g => new ArchiveLink { Path = g.Key, Label = g.First().ProvinceName, Region = g.First().Region,
                        ProvinceCode = g.First().ProvinceCode, ProvinceName = g.First().ProvinceName, Modified = g.Max(x => x.Modified) }).ToList());
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT v.GameCode, v.DrawNo, v.DrawDate FROM dbo.VietlottResults v
                        WHERE v.DrawDate < @tomorrow AND (NULLIF(LTRIM(RTRIM(v.Numbers)), '') IS NOT NULL
                        OR EXISTS (SELECT 1 FROM dbo.VietlottPrizes p WHERE p.ResultId = v.Id AND NULLIF(LTRIM(RTRIM(p.Numbers)), '') IS NOT NULL))";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "@tomorrow";
                    parameter.Value = LotteryUrls.Today.AddDays(1); command.Parameters.Add(parameter);
                    await using var reader = await command.ExecuteReaderAsync(cancellation);
                    while (await reader.ReadAsync(cancellation))
                    {
                        var game = reader.GetString(0); var draw = reader.GetString(1);
                        if (!SupportedGames.Contains(game) || string.IsNullOrWhiteSpace(draw)) continue;
                        entries.Add(new ArchiveLink { GameCode = game, DrawNo = draw, Date = reader.GetDateTime(2),
                            Path = LotteryUrls.DrawPath(game, draw), Label = LotteryUrls.GameName(game) + " · kỳ " + draw });
                    }
                }
                entries.AddRange(entries.Where(x => x.GameCode != null).Select(x => x.GameCode).Distinct()
                    .Select(game => new ArchiveLink { GameCode = game, Path = LotteryUrls.GamePath(game), Label = LotteryUrls.GameName(game) }).ToList());
                entries = entries.GroupBy(x => x.Path, StringComparer.Ordinal).Select(g => g.OrderByDescending(x => x.Modified).First()).ToList();
                cache.Set("archive-catalog", entries, TimeSpan.FromMinutes(2));
                return entries;
            }
            finally { gate.Release(); }
        }
        public static bool HasResultNumbers(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return prizes != null && prizes.All(x => x != null && x.PrizeName != null && x.Numbers != null)
                    && prizes.Any(x => x.Numbers.Any(number => !string.IsNullOrWhiteSpace(number)));
            }
            catch (JsonException) { return false; }
        }
        public static readonly string[] SupportedGames = { "BINGO18", "KENO", "LOTTO535", "MAX3D", "MAX3D_PLUS", "MAX3D_PRO", "MEGA645", "POWER655" };
    }
}
