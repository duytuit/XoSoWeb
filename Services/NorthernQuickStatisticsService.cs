using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public sealed class NorthernQuickStatisticsService
    {
        private readonly XoSoDBContext db;
        private readonly IMemoryCache cache;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        public NorthernQuickStatisticsService(XoSoDBContext db, IMemoryCache cache) { this.db = db; this.cache = cache; }

        public Task<NorthernQuickStatisticsViewModel> GetAsync(DateTime forDate, CancellationToken cancellation) =>
            cache.GetOrCreateAsync("northern-quick-statistics-v1-" + PublicContentVersion.Value + "-" + forDate.ToString("yyyyMMdd"), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var draws = new List<NumberRhythmDay>();
                var seen = new HashSet<DateTime>();
                await db.Database.OpenConnectionAsync(cancellation);
                try
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = @"SELECT DrawDate, Prizes FROM dbo.XoSoResults
                        WHERE DrawDate >= @quickStart AND DrawDate < @quickEnd
                        AND Region IN (N'MB', N'Miền bắc', N'Mien Bac', N'Miền bac')
                        ORDER BY DrawDate DESC, UpdatedAt DESC, Id DESC";
                    var start = command.CreateParameter(); start.ParameterName = "@quickStart"; start.Value = forDate.Date.AddDays(-NorthernQuickStatisticsCalculator.HistoryDays); command.Parameters.Add(start);
                    var end = command.CreateParameter(); end.ParameterName = "@quickEnd"; end.Value = forDate.Date; command.Parameters.Add(end);
                    await using var reader = await command.ExecuteReaderAsync(cancellation);
                    while (await reader.ReadAsync(cancellation))
                    {
                        var date = reader.GetDateTime(0).Date;
                        if (seen.Contains(date) || reader.IsDBNull(1)) continue;
                        List<XoSoPrize> prizes;
                        try { prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(1), JsonOptions); }
                        catch (JsonException) { continue; }
                        var day = NumberRhythmCalculator.CreateDay(date, prizes);
                        if (day == null) continue;
                        seen.Add(date); draws.Add(day);
                    }
                }
                finally { await db.Database.CloseConnectionAsync(); }
                return NorthernQuickStatisticsCalculator.Calculate(forDate, draws);
            });
    }
}
