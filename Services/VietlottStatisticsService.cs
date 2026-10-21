using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public sealed class VietlottStatisticsService
    {
        private const int QueryDrawLimit = 400;
        private static readonly int[] PeriodPresets = { 20, 50, 100, 200 };
        private static readonly string[] StatisticsGameOrder =
        {
            "MEGA645", "POWER655", "LOTTO535", "KENO", "MAX3D", "MAX3D_PLUS", "MAX3D_PRO", "BINGO18"
        };
        private static readonly Regex ThreeDigitNumber = new Regex(@"\A[0-9]{3}\z", RegexOptions.Compiled);
        private static readonly Dictionary<string, VietlottStatisticsToolViewModel[]> ToolCatalog =
            new Dictionary<string, VietlottStatisticsToolViewModel[]>(StringComparer.Ordinal)
            {
                ["MEGA645"] = Tools(
                    ("tan-suat", "Tần suất", "Số lần và số kỳ xuất hiện từng số 01–45."),
                    ("chua-xuat-hien", "Chưa xuất hiện", "Số kỳ hoàn chỉnh kể từ lần xuất hiện gần nhất."),
                    ("theo-ky", "Theo kỳ", "Bộ số và mã kỳ quay theo lịch sử."),
                    ("cap-so", "Cặp số", "Các cặp số cùng xuất hiện trong một kỳ."),
                    ("chan-le", "Chẵn/lẻ", "Phân bố số lượng số chẵn và lẻ trong bộ kết quả."),
                    ("tong-bo-so", "Tổng bộ số", "Phân bố tổng sáu số của mỗi kỳ."),
                    ("lien-tiep", "Số liên tiếp", "Các cặp số liên tiếp cùng xuất hiện trong bộ kết quả."),
                    ("trung-ky-truoc", "Trùng kỳ trước", "Số lượng số chung với kỳ liền trước trong dữ liệu."),
                    ("jackpot", "Jackpot", "Lịch sử Jackpot ước tính được lưu cùng kỳ quay.")),
                ["POWER655"] = Tools(
                    ("tan-suat", "Tần suất", "Số lần và số kỳ xuất hiện từng số chính; không tính số đặc biệt."),
                    ("chua-xuat-hien", "Chưa xuất hiện", "Số kỳ chính hoàn chỉnh kể từ lần xuất hiện gần nhất."),
                    ("theo-ky", "Theo kỳ", "Bộ số chính, số đặc biệt và mã kỳ quay."),
                    ("cap-so", "Cặp số", "Các cặp số chính cùng xuất hiện trong một kỳ."),
                    ("chan-le", "Chẵn/lẻ", "Phân bố chẵn/lẻ chỉ trong bộ số chính."),
                    ("tong-bo-so", "Tổng bộ số", "Phân bố tổng sáu số chính, không gồm số đặc biệt."),
                    ("lien-tiep", "Số liên tiếp", "Các cặp số chính liên tiếp cùng xuất hiện."),
                    ("trung-ky-truoc", "Trùng kỳ trước", "Số lượng số chính chung với kỳ liền trước."),
                    ("so-dac-biet", "Số đặc biệt", "Tần suất số đặc biệt riêng, không gộp vào bộ số chính."),
                    ("jackpot", "Jackpot", "Jackpot 1/2 ước tính được lưu cùng kỳ quay; không phải tiền đã trả.")),
                ["LOTTO535"] = Tools(
                    ("tan-suat", "Tần suất", "Tần suất riêng của năm số chính theo kết quả hoàn chỉnh."),
                    ("chua-xuat-hien", "Chưa xuất hiện", "Số kỳ chính hoàn chỉnh kể từ lần xuất hiện gần nhất."),
                    ("theo-ky", "Theo kỳ", "Bộ số chính, số đặc biệt, mã kỳ và ngày quay."),
                    ("cap-so", "Cặp số", "Các cặp số chính cùng xuất hiện trong một kỳ."),
                    ("chan-le", "Chẵn/lẻ", "Phân bố số lượng số chẵn/lẻ trong bộ số chính."),
                    ("tong-bo-so", "Tổng bộ số", "Phân bố tổng năm số chính của mỗi kỳ."),
                    ("so-dac-biet", "Số đặc biệt", "Tần suất số đặc biệt riêng, không gộp với số chính.")),
                ["KENO"] = Tools(
                    ("tan-suat", "Tần suất", "Số lần và số kỳ xuất hiện từng số 01–80."),
                    ("chua-xuat-hien", "Chưa xuất hiện", "Số kỳ hoàn chỉnh kể từ lần xuất hiện gần nhất."),
                    ("theo-ky", "Theo kỳ", "20 số và mã kỳ quay; không suy diễn thứ tự quay."),
                    ("cap-so", "Cặp số", "Các cặp cùng xuất hiện trong một kỳ."),
                    ("chan-le", "Chẵn/lẻ", "Số lượng số chẵn và lẻ trong từng kỳ."),
                    ("lon-nho", "Lớn/nhỏ", "Phân loại theo quy tắc công bố: nhỏ 01–40, lớn 41–80."),
                    ("trung-ky-truoc", "Trùng kỳ trước", "Số lượng số chung với kỳ liền trước trong dữ liệu.")),
                ["MAX3D"] = Max3dTools(),
                ["MAX3D_PLUS"] = Max3dTools(),
                ["MAX3D_PRO"] = Max3dTools(),
                ["BINGO18"] = Tools(
                    ("tong", "Tổng", "Phân bố tổng ba giá trị lưu trong mỗi kỳ."),
                    ("chan-le", "Chẵn/lẻ", "Chẵn/lẻ của tổng kết quả mỗi kỳ."),
                    ("bo-ket-qua", "Bộ kết quả", "Tần suất chuỗi kết quả đúng như được lưu; không khẳng định thứ tự quay."),
                    ("theo-ky", "Theo kỳ", "Lịch sử kết quả theo mã kỳ và ngày quay."))
            };

        private readonly XoSoDBContext db;
        private readonly IMemoryCache cache;

        public VietlottStatisticsService(XoSoDBContext db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        public async Task<VietlottStatisticsIndexViewModel> GetIndexAsync(CancellationToken cancellation)
        {
            return await cache.GetOrCreateAsync("vietlott-statistics-index", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await BuildIndexAsync(cancellation);
            });
        }

        private async Task<VietlottStatisticsIndexViewModel> BuildIndexAsync(CancellationToken cancellation)
        {
            var games = new List<VietlottStatisticsGameViewModel>();
            foreach (var game in StatisticsGameOrder)
            {
                var data = await GetDrawsAsync(game, PeriodPresets[0], null, cancellation);
                var available = GetAvailableTools(game, data.Draws);
                if (available.Count == 0) continue;
                games.Add(new VietlottStatisticsGameViewModel
                {
                    GameCode = game,
                    Slug = LotteryUrls.Slug(game),
                    Name = DisplayGameName(game),
                    Tools = available
                });
            }
            return new VietlottStatisticsIndexViewModel { Games = games };
        }

        public async Task<VietlottStatisticsGameViewModel> GetGameAsync(string slug, CancellationToken cancellation)
        {
            var game = GameCodeFromSlug(slug);
            if (game == null) return null;
            var data = await GetDrawsAsync(game, PeriodPresets[0], null, cancellation);
            var tools = GetAvailableTools(game, data.Draws);
            if (tools.Count == 0) return null;
            return new VietlottStatisticsGameViewModel
            {
                GameCode = game,
                Slug = slug,
                Name = DisplayGameName(game),
                Tools = tools
            };
        }

        public async Task<VietlottStatisticsPageViewModel> GetPageAsync(
            string gameSlug, string toolSlug, int periods, string endDraw, string prizeName, string sort, string search, int page,
            CancellationToken cancellation)
        {
            prizeName = string.IsNullOrWhiteSpace(prizeName) ? null : prizeName.Trim();
            var game = GameCodeFromSlug(gameSlug);
            var tool = ToolCatalog.TryGetValue(game ?? string.Empty, out var catalog)
                ? catalog.FirstOrDefault(item => item.Slug == toolSlug)
                : null;
            if (game == null || tool == null || !PeriodPresets.Contains(periods) || page < 1 || page > 100000
                || (sort != "number" && sort != "frequency" && sort != "gap")
                || (search != null && search.Length > 30)
                || (prizeName != null && prizeName.Length > 100)
                || (endDraw != null && !LotteryUrls.ValidDrawNo(endDraw))) return null;

            var cacheKey = "vietlott-stat-" + game + "-" + toolSlug + "-" + periods + "-" + (endDraw ?? "latest")
                + "-" + (prizeName ?? "all");
            var cached = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return await BuildPageAsync(game, tool, periods, endDraw, prizeName, cancellation);
            });
            if (cached == null || !cached.Tools.Any(item => item.Slug == toolSlug)) return null;
            var computed = new VietlottStatisticsPageViewModel
            {
                GameCode = cached.GameCode,
                GameSlug = cached.GameSlug,
                GameName = cached.GameName,
                ToolSlug = cached.ToolSlug,
                ToolName = cached.ToolName,
                Description = cached.Description,
                EndDraw = cached.EndDraw,
                PrizeName = cached.PrizeName,
                RequestedPeriods = cached.RequestedPeriods,
                UsedPeriods = cached.UsedPeriods,
                ExcludedPeriods = cached.ExcludedPeriods,
                OldestDrawDate = cached.OldestDrawDate,
                NewestDrawDate = cached.NewestDrawDate,
                PeriodPresets = cached.PeriodPresets,
                DrawOptions = cached.DrawOptions,
                PrizeOptions = cached.PrizeOptions,
                Tools = cached.Tools,
                Rows = cached.Rows.ToList(),
                IsJackpotEstimate = cached.IsJackpotEstimate,
                IsMax3d = cached.IsMax3d,
                IsDrawHistory = cached.IsDrawHistory,
                HasDrawReferences = cached.HasDrawReferences,
                PageSize = cached.PageSize
            };

            computed.Sort = sort;
            computed.Search = search;
            computed.Page = page;
            var rows = computed.Rows.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
                rows = rows.Where(row => (row.Value ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (row.Detail ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (row.DrawNo ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
            rows = sort == "frequency"
                ? rows.OrderByDescending(row => row.Count).ThenBy(row => row.Value, StringComparer.Ordinal)
                : sort == "gap"
                    ? rows.OrderByDescending(row => row.Gap ?? -1).ThenBy(row => row.Value, StringComparer.Ordinal)
                    : rows.OrderBy(row => row.SortValue).ThenBy(row => row.Value, StringComparer.Ordinal);
            var ordered = rows.ToList();
            var lastPage = Math.Max(1, (ordered.Count + computed.PageSize - 1) / computed.PageSize);
            if (computed.Page > lastPage) computed.Page = lastPage;
            computed.TotalRows = ordered.Count;
            computed.PageRows = ordered.Skip((computed.Page - 1) * computed.PageSize).Take(computed.PageSize).ToList();
            computed.HasDrawReferences = computed.PageRows.Any(row => !string.IsNullOrWhiteSpace(row.DrawUrl));
            return computed;
        }

        private async Task<VietlottStatisticsPageViewModel> BuildPageAsync(
            string game, VietlottStatisticsToolViewModel tool, int periods, string endDraw, string prizeName, CancellationToken cancellation)
        {
            var data = await GetDrawsAsync(game, periods, endDraw, cancellation);
            var availableTools = GetAvailableTools(game, data.Draws);
            if (!availableTools.Any(item => item.Slug == tool.Slug)) return null;

            var selected = data.Draws.Where(draw => draw.IsComplete).Take(periods).ToList();
            var prizeOptions = game.StartsWith("MAX3D", StringComparison.Ordinal)
                ? selected.SelectMany(draw => draw.Prizes.Where(prize => prize.Numbers.Any(value => ThreeDigitNumber.IsMatch(value)))
                    .Select(prize => prize.Name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            if (prizeName != null && !prizeOptions.Any(name => string.Equals(name, prizeName, StringComparison.OrdinalIgnoreCase))) return null;
            var oldestSelectedId = selected.Count == 0 ? (long?)null : selected[selected.Count - 1].Id;
            var considered = oldestSelectedId.HasValue
                ? data.Draws.TakeWhile(draw => draw.Id != oldestSelectedId.Value).Concat(data.Draws.Where(draw => draw.Id == oldestSelectedId.Value).Take(1)).ToList()
                : new List<Draw>();
            var model = new VietlottStatisticsPageViewModel
            {
                GameCode = game,
                GameSlug = LotteryUrls.Slug(game),
                GameName = DisplayGameName(game),
                ToolSlug = tool.Slug,
                ToolName = tool.Name,
                Description = tool.Description,
                EndDraw = endDraw,
                PrizeName = prizeName,
                RequestedPeriods = periods,
                UsedPeriods = selected.Count,
                ExcludedPeriods = considered.Count(draw => !draw.IsComplete),
                OldestDrawDate = selected.Count == 0 ? null : selected[selected.Count - 1].DrawDate,
                NewestDrawDate = selected.Count == 0 ? null : selected[0].DrawDate,
                Tools = availableTools,
                IsJackpotEstimate = tool.Slug == "jackpot",
                IsMax3d = game.StartsWith("MAX3D", StringComparison.Ordinal),
                IsDrawHistory = tool.Slug == "theo-ky",
                PeriodPresets = PeriodPresets.ToList(),
                DrawOptions = data.Draws.Where(draw => draw.IsComplete).Take(200)
                    .Select(draw => new VietlottStatisticsDrawOption { DrawNo = draw.DrawNo, DrawDate = draw.DrawDate }).ToList(),
                PrizeOptions = prizeOptions
            };
            model.Rows = Calculate(game, tool.Slug, selected, considered, data.Draws, prizeName);
            return model;
        }

        private async Task<DrawLoadResult> GetDrawsAsync(string game, int periods, string endDraw, CancellationToken cancellation)
        {
            var draws = new Dictionary<long, Draw>();
            var connection = db.Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere) await connection.OpenAsync(cancellation);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
                    WITH Candidate AS
                    (
                        SELECT TOP (@limit) r.Id
                        FROM dbo.VietlottResults r
                        WHERE r.GameCode = @game AND r.DrawDate < @tomorrow
                          AND (@endDraw IS NULL OR EXISTS
                          (
                              SELECT 1 FROM dbo.VietlottResults boundary
                              WHERE boundary.GameCode = @game AND boundary.DrawNo = @endDraw
                                AND boundary.DrawDate < @tomorrow
                                AND (r.DrawDate < boundary.DrawDate OR (r.DrawDate = boundary.DrawDate AND r.Id <= boundary.Id))
                          ))
                        ORDER BY r.DrawDate DESC, r.Id DESC
                    )
                    SELECT r.Id, r.GameCode, r.DrawNo, r.DrawDate, r.Numbers, r.SpecialNumbers,
                           r.Total, r.Size, r.OddEven, r.EstimatedPrize, r.EstimatedPrize2,
                           p.PrizeName, p.Numbers, p.PrizeValue
                    FROM Candidate c
                    JOIN dbo.VietlottResults r ON r.Id = c.Id
                    LEFT JOIN dbo.VietlottPrizes p ON p.ResultId = r.Id
                    ORDER BY r.DrawDate DESC, r.Id DESC, p.Id;";
                AddParameter(command, "@limit", QueryDrawLimit);
                AddParameter(command, "@game", game);
                AddParameter(command, "@tomorrow", LotteryUrls.Today.AddDays(1));
                AddParameter(command, "@endDraw", endDraw);
                await using var reader = await command.ExecuteReaderAsync(cancellation);
                while (await reader.ReadAsync(cancellation))
                {
                    var id = reader.GetInt64(0);
                    if (!draws.TryGetValue(id, out var draw))
                    {
                        draw = new Draw
                        {
                            Id = id,
                            GameCode = reader.GetString(1),
                            DrawNo = reader.GetString(2),
                            DrawDate = reader.GetDateTime(3),
                            Numbers = SplitValues(reader.IsDBNull(4) ? null : reader.GetString(4)),
                            SpecialNumbers = SplitValues(reader.IsDBNull(5) ? null : reader.GetString(5)),
                            Total = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            Size = reader.IsDBNull(7) ? null : reader.GetString(7),
                            OddEven = reader.IsDBNull(8) ? null : reader.GetString(8),
                            Jackpot1 = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                            Jackpot2 = reader.IsDBNull(10) ? null : reader.GetInt64(10)
                        };
                        draws.Add(id, draw);
                    }
                    if (!reader.IsDBNull(11))
                        draw.Prizes.Add(new Prize
                        {
                            Name = reader.GetString(11),
                            Numbers = SplitPrizeValues(reader.IsDBNull(12) ? null : reader.GetString(12)),
                            Value = reader.IsDBNull(13) ? null : reader.GetString(13)
                        });
                }
                foreach (var draw in draws.Values) draw.IsComplete = IsComplete(draw);
            }
            finally
            {
                if (openedHere) await connection.CloseAsync();
            }
            return new DrawLoadResult { Draws = draws.Values.OrderByDescending(draw => draw.DrawDate).ThenByDescending(draw => draw.Id).ToList() };
        }

        private static List<VietlottStatisticsToolViewModel> GetAvailableTools(string game, List<Draw> draws)
        {
            if (!ToolCatalog.TryGetValue(game, out var tools)) return new List<VietlottStatisticsToolViewModel>();
            var complete = draws.Where(draw => draw.IsComplete).ToList();
            if (complete.Count == 0) return new List<VietlottStatisticsToolViewModel>();
            var isMax3d = game.StartsWith("MAX3D", StringComparison.Ordinal);
            var hasTriples = complete.SelectMany(draw => draw.Prizes).SelectMany(prize => prize.Numbers).Any(value => ThreeDigitNumber.IsMatch(value));
            return tools.Where(tool =>
            {
                if (tool.Slug == "jackpot") return complete.Any(draw => draw.Jackpot1.HasValue || draw.Jackpot2.HasValue);
                if (isMax3d) return tool.Slug == "theo-ky" || hasTriples;
                return true;
            }).ToList();
        }

        private static List<VietlottStatisticsRow> Calculate(string game, string tool, List<Draw> selected, List<Draw> considered, List<Draw> allLoaded, string prizeName)
        {
            var rows = new List<VietlottStatisticsRow>();
            var isMax3d = game.StartsWith("MAX3D", StringComparison.Ordinal);
            if (tool == "theo-ky")
            {
                foreach (var draw in selected)
                    rows.Add(new VietlottStatisticsRow
                    {
                        Value = string.Join(" ", draw.Numbers),
                        Detail = draw.SpecialNumbers.Count == 0 ? null : "Số đặc biệt: " + string.Join(" ", draw.SpecialNumbers),
                        Count = 1,
                        DrawCount = 1,
                        DrawNo = draw.DrawNo,
                        DrawDate = draw.DrawDate,
                        DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo)
                    });
                if (isMax3d)
                {
                    foreach (var row in rows)
                    {
                        var draw = selected.First(item => item.DrawNo == row.DrawNo);
                        row.Value = string.Join("; ", draw.Prizes.Select(prize => prize.Name + ": " + string.Join(", ", prize.Numbers)));
                    }
                }
                return rows;
            }
            if (tool == "jackpot")
            {
                foreach (var draw in selected.Where(item => item.Jackpot1.HasValue || item.Jackpot2.HasValue))
                    rows.Add(new VietlottStatisticsRow
                    {
                        Value = (draw.Jackpot1.HasValue ? "Jackpot 1 ước tính: " + draw.Jackpot1.Value.ToString("N0", CultureInfo.InvariantCulture) + " đ" : null)
                            + (draw.Jackpot2.HasValue ? (draw.Jackpot1.HasValue ? " · " : "") + "Jackpot 2 ước tính: " + draw.Jackpot2.Value.ToString("N0", CultureInfo.InvariantCulture) + " đ" : ""),
                        Count = 1,
                        DrawCount = 1,
                        DrawNo = draw.DrawNo,
                        DrawDate = draw.DrawDate,
                        DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo)
                    });
                return rows;
            }
            if (isMax3d)
                return CalculateMax3d(tool, selected, game, prizeName);

            if (tool == "so-dac-biet")
                return BuildFrequencyRows(selected, draw => draw.SpecialNumbers);

            if (tool == "tan-suat" || tool == "chua-xuat-hien")
            {
                var range = game == "MEGA645" ? Enumerable.Range(1, 45)
                    : game == "POWER655" ? Enumerable.Range(1, 55)
                    : game == "LOTTO535" ? Enumerable.Range(1, 35)
                    : Enumerable.Range(1, 80);
                var frequencies = CountNumbers(selected, draw => draw.Numbers);
                foreach (var value in range)
                {
                    var number = value.ToString(game == "BINGO18" ? "0" : "00", CultureInfo.InvariantCulture);
                    var last = selected.FindIndex(draw => draw.Numbers.Contains(number) || draw.Numbers.Contains(value.ToString(CultureInfo.InvariantCulture)));
                    rows.Add(new VietlottStatisticsRow
                    {
                        Value = number,
                        Count = frequencies.TryGetValue(number, out var count) ? count.Occurrences : 0,
                        DrawCount = frequencies.TryGetValue(number, out count) ? count.Draws : 0,
                        Gap = last < 0 ? selected.Count : last,
                        SortValue = value,
                        DrawNo = last < 0 ? null : selected[last].DrawNo,
                        DrawDate = last < 0 ? null : selected[last].DrawDate,
                        DrawUrl = last < 0 ? null : LotteryUrls.DrawPath(game, selected[last].DrawNo)
                    });
                }
                if (tool == "chua-xuat-hien")
                    rows.ForEach(row => row.Count = row.Gap ?? 0);
                return rows;
            }
            if (tool == "cap-so")
            {
                var pairs = new Dictionary<string, HashSet<long>>();
                foreach (var draw in selected)
                {
                    var values = ParseNumbers(draw.Numbers).Distinct().OrderBy(number => number).ToList();
                    for (var i = 0; i < values.Count; i++)
                    for (var j = i + 1; j < values.Count; j++)
                    {
                        var key = values[i].ToString("00", CultureInfo.InvariantCulture) + "–" + values[j].ToString("00", CultureInfo.InvariantCulture);
                        if (!pairs.TryGetValue(key, out var drawIds)) pairs[key] = drawIds = new HashSet<long>();
                        drawIds.Add(draw.Id);
                    }
                }
                return pairs.Select(item =>
                {
                    var row = new VietlottStatisticsRow { Value = item.Key, Count = item.Value.Count, DrawCount = item.Value.Count };
                    SetLatestDrawReference(row, selected.Where(draw => item.Value.Contains(draw.Id)));
                    return row;
                }).ToList();
            }
            if (tool == "chan-le")
            {
                foreach (var draw in selected)
                {
                    var nums = ParseNumbers(draw.Numbers);
                    if (game == "BINGO18")
                    {
                        if (draw.Total.HasValue)
                            rows.Add(new VietlottStatisticsRow { Value = draw.Total.Value % 2 == 0 ? "Tổng chẵn" : "Tổng lẻ", Count = 1, DrawCount = 1, DrawNo = draw.DrawNo, DrawDate = draw.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo) });
                    }
                    else
                        rows.Add(new VietlottStatisticsRow { Value = "Chẵn " + nums.Count(value => value % 2 == 0) + " – lẻ " + nums.Count(value => value % 2 != 0), Count = 1, DrawCount = 1, DrawNo = draw.DrawNo, DrawDate = draw.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo) });
                }
                return GroupRows(rows);
            }
            if (tool == "tong-bo-so" || tool == "tong" || tool == "bo-ket-qua")
            {
                foreach (var draw in selected)
                {
                    var values = tool == "bo-ket-qua" ? string.Join(" ", draw.Numbers) : (draw.Total ?? ParseNumbers(draw.Numbers).Sum()).ToString(CultureInfo.InvariantCulture);
                    rows.Add(new VietlottStatisticsRow { Value = values, Count = 1, DrawCount = 1, DrawNo = draw.DrawNo, DrawDate = draw.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo) });
                }
                return GroupRows(rows);
            }
            if (tool == "lon-nho")
            {
                foreach (var draw in selected)
                {
                    var numbers = ParseNumbers(draw.Numbers);
                    rows.Add(new VietlottStatisticsRow
                    {
                        Value = "Nhỏ 01–40: " + numbers.Count(number => number <= 40) + " · Lớn 41–80: " + numbers.Count(number => number >= 41),
                        Count = 1, DrawCount = 1, DrawNo = draw.DrawNo, DrawDate = draw.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo)
                    });
                }
                return GroupRows(rows);
            }
            if (tool == "lien-tiep")
            {
                var consecutive = new Dictionary<string, HashSet<long>>();
                foreach (var draw in selected)
                {
                    var values = ParseNumbers(draw.Numbers).Distinct().OrderBy(number => number).ToList();
                    for (var index = 1; index < values.Count; index++)
                        if (values[index] == values[index - 1] + 1)
                        {
                            var key = values[index - 1].ToString("00", CultureInfo.InvariantCulture) + "–" + values[index].ToString("00", CultureInfo.InvariantCulture);
                            if (!consecutive.TryGetValue(key, out var drawIds)) consecutive[key] = drawIds = new HashSet<long>();
                            drawIds.Add(draw.Id);
                        }
                }
                return consecutive.Select(item =>
                {
                    var row = new VietlottStatisticsRow { Value = item.Key, Count = item.Value.Count, DrawCount = item.Value.Count };
                    SetLatestDrawReference(row, selected.Where(draw => item.Value.Contains(draw.Id)));
                    return row;
                }).ToList();
            }
            if (tool == "trung-ky-truoc")
            {
                var ordered = considered.Where(draw => draw.IsComplete).ToList();
                for (var index = 0; index + 1 < ordered.Count; index++)
                {
                    var newer = ordered[index];
                    var older = ordered[index + 1];
                    if (!IsAdjacentSourceDraw(considered, newer, older)) continue;
                    var previous = new HashSet<string>(older.Numbers, StringComparer.Ordinal);
                    var repeated = newer.Numbers.Where(previous.Contains).Distinct().Count();
                    rows.Add(new VietlottStatisticsRow { Value = repeated + " số trùng", Count = 1, DrawCount = 1, DrawNo = newer.DrawNo, DrawDate = newer.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, newer.DrawNo), Detail = "Kỳ liền trước: " + older.DrawNo });
                }
                return GroupRows(rows);
            }
            return rows;
        }

        private static List<VietlottStatisticsRow> CalculateMax3d(string tool, List<Draw> selected, string game, string prizeName)
        {
            var triples = selected.SelectMany(draw => draw.Prizes
                .Where(prize => prizeName == null || string.Equals(prize.Name, prizeName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(prize => prize.Numbers
                .Where(value => ThreeDigitNumber.IsMatch(value)).Select(value => new TripleObservation(draw, prize.Name, value)))).ToList();
            if (tool == "theo-giai")
                return triples.GroupBy(item => item.PrizeName, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var row = new VietlottStatisticsRow { Value = group.Key, Count = group.Count(), DrawCount = group.Select(item => item.Draw.Id).Distinct().Count() };
                        SetLatestDrawReference(row, group.Select(item => item.Draw));
                        return row;
                    }).ToList();
            if (tool == "theo-ky")
                return selected.Select(draw => new VietlottStatisticsRow
                {
                    Value = string.Join("; ", draw.Prizes.Select(prize => prize.Name + ": " + string.Join(", ", prize.Numbers.Where(value => ThreeDigitNumber.IsMatch(value))))),
                    Count = 1, DrawCount = 1, DrawNo = draw.DrawNo, DrawDate = draw.DrawDate, DrawUrl = LotteryUrls.DrawPath(game, draw.DrawNo)
                }).ToList();
            var numberRange = tool == "hai-so-cuoi" ? Enumerable.Range(0, 100).Select(value => value.ToString("00", CultureInfo.InvariantCulture))
                : tool == "chu-so-theo-vi-tri" ? Enumerable.Range(0, 10).Select(value => value.ToString(CultureInfo.InvariantCulture))
                : Enumerable.Range(0, 1000).Select(value => value.ToString("000", CultureInfo.InvariantCulture));
            var counts = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            var occurrences = new Dictionary<string, long>(StringComparer.Ordinal);
            var lastAppearance = new Dictionary<string, int>(StringComparer.Ordinal);
            var numberIndex = new Dictionary<long, int>();
            for (var i = 0; i < selected.Count; i++) numberIndex[selected[i].Id] = i;
            foreach (var item in triples)
            {
                var keys = tool switch
                {
                    "hai-so-cuoi" => new[] { item.Number.Substring(1) },
                    "chu-so-theo-vi-tri" => new[]
                    {
                        "Hàng trăm: " + item.Number[0], "Hàng chục: " + item.Number[1], "Hàng đơn vị: " + item.Number[2]
                    },
                    _ => new[] { item.Number }
                };
                foreach (var key in keys)
                {
                    if (!counts.TryGetValue(key, out var drawIds)) counts[key] = drawIds = new HashSet<long>();
                    drawIds.Add(item.Draw.Id);
                    occurrences.TryGetValue(key, out var occurrenceCount);
                    occurrences[key] = occurrenceCount + 1;
                    if (!lastAppearance.ContainsKey(key) || numberIndex[item.Draw.Id] < lastAppearance[key]) lastAppearance[key] = numberIndex[item.Draw.Id];
                }
            }
            var max3dRows = new List<VietlottStatisticsRow>();
            foreach (var value in (tool == "chu-so-theo-vi-tri"
                         ? new[] { "Hàng trăm: ", "Hàng chục: ", "Hàng đơn vị: " }.SelectMany(prefix => Enumerable.Range(0, 10).Select(digit => prefix + digit))
                         : numberRange))
            {
                var found = counts.TryGetValue(value, out var drawIds);
                max3dRows.Add(new VietlottStatisticsRow
                {
                    Value = value,
                    Count = found ? occurrences[value] : 0,
                    DrawCount = found ? drawIds.Count : 0,
                    Gap = lastAppearance.TryGetValue(value, out var index) ? index : selected.Count,
                    SortValue = int.TryParse(value, out var numeric) ? numeric : 0
                });
                if (found)
                    SetLatestDrawReference(max3dRows[max3dRows.Count - 1], selected.Where(draw => drawIds.Contains(draw.Id)));
            }
            return max3dRows;
        }

        private static Dictionary<string, (int Draws, int Occurrences)> CountNumbers(List<Draw> draws, Func<Draw, List<string>> selectNumbers)
        {
            var counts = new Dictionary<string, (int Draws, int Occurrences)>(StringComparer.Ordinal);
            foreach (var draw in draws)
                foreach (var number in selectNumbers(draw))
                {
                    var key = int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed.ToString("00", CultureInfo.InvariantCulture) : number;
                    counts.TryGetValue(key, out var value);
                    counts[key] = (value.Draws + 1, value.Occurrences + 1);
                }
            return counts;
        }

        private static List<VietlottStatisticsRow> BuildFrequencyRows(List<Draw> draws, Func<Draw, List<string>> selectNumbers)
        {
            var rows = new Dictionary<string, (int Occurrences, HashSet<long> DrawIds)>(StringComparer.Ordinal);
            foreach (var draw in draws)
                foreach (var value in selectNumbers(draw))
                {
                    if (!rows.TryGetValue(value, out var counts)) counts = (0, new HashSet<long>());
                    counts.Occurrences++;
                    counts.DrawIds.Add(draw.Id);
                    rows[value] = counts;
                }
            return rows.Select(item =>
            {
                var row = new VietlottStatisticsRow
                {
                    Value = item.Key,
                    Count = item.Value.Occurrences,
                    DrawCount = item.Value.DrawIds.Count,
                    SortValue = long.TryParse(item.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0
                };
                SetLatestDrawReference(row, draws.Where(draw => item.Value.DrawIds.Contains(draw.Id)));
                return row;
            }).ToList();
        }

        private static void SetLatestDrawReference(VietlottStatisticsRow row, IEnumerable<Draw> draws)
        {
            var latest = draws.OrderByDescending(draw => draw.DrawDate).ThenByDescending(draw => draw.Id).FirstOrDefault();
            if (latest == null) return;
            row.DrawNo = latest.DrawNo;
            row.DrawDate = latest.DrawDate;
            row.DrawUrl = LotteryUrls.DrawPath(latest.GameCode, latest.DrawNo);
        }

        private static List<VietlottStatisticsRow> GroupRows(List<VietlottStatisticsRow> rows) =>
            rows.GroupBy(row => row.Value, StringComparer.Ordinal)
                .Select(group =>
                {
                    var latest = group.Where(item => item.DrawUrl != null)
                        .OrderByDescending(item => item.DrawDate).FirstOrDefault();
                    var row = new VietlottStatisticsRow
                    {
                        Value = group.Key,
                        Count = group.Count(),
                        DrawCount = group.Count(),
                        SortValue = long.TryParse(group.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0
                    };
                    if (latest != null)
                    {
                        row.DrawNo = latest.DrawNo;
                        row.DrawDate = latest.DrawDate;
                        row.DrawUrl = latest.DrawUrl;
                    }
                    return row;
                }).ToList();

        private static bool IsAdjacentSourceDraw(List<Draw> considered, Draw newer, Draw older)
        {
            var newerIndex = considered.FindIndex(draw => draw.Id == newer.Id);
            return newerIndex >= 0 && newerIndex + 1 < considered.Count && considered[newerIndex + 1].Id == older.Id
                && int.TryParse(newer.DrawNo, NumberStyles.None, CultureInfo.InvariantCulture, out var newerNumber)
                && int.TryParse(older.DrawNo, NumberStyles.None, CultureInfo.InvariantCulture, out var olderNumber)
                && newerNumber - olderNumber == 1;
        }

        private static bool IsComplete(Draw draw)
        {
            if (!LotteryUrls.ValidDrawNo(draw.DrawNo)) return false;
            if (draw.GameCode == "MEGA645") return InRangeUnique(draw.Numbers, 6, 1, 45) && draw.SpecialNumbers.Count == 0;
            if (draw.GameCode == "POWER655") return InRangeUnique(draw.Numbers, 6, 1, 55)
                && InRangeUnique(draw.SpecialNumbers, 1, 1, 55)
                && !ParseNumbers(draw.Numbers).Intersect(ParseNumbers(draw.SpecialNumbers)).Any();
            if (draw.GameCode == "LOTTO535") return InRangeUnique(draw.Numbers, 5, 1, 35)
                && draw.SpecialNumbers.Count == 1 && draw.SpecialNumbers.All(value => int.TryParse(value, out _));
            if (draw.GameCode == "KENO") return InRangeUnique(draw.Numbers, 20, 1, 80) && draw.SpecialNumbers.Count == 0;
            if (draw.GameCode == "BINGO18")
            {
                if (draw.Numbers.Count != 3 || draw.SpecialNumbers.Count != 0
                    || draw.Numbers.Any(value => !int.TryParse(value, out var number) || number < 1 || number > 6)) return false;
                var total = draw.Numbers.Sum(value => int.Parse(value, CultureInfo.InvariantCulture));
                return !draw.Total.HasValue || draw.Total.Value == total;
            }
            var requiredPrizeTypes = draw.GameCode == "MAX3D"
                ? new[] { "dac biet", "nhat", "nhi", "ba" }
                : draw.GameCode == "MAX3D_PLUS"
                    ? new[] { "dac biet", "nhat", "nhi", "ba", "tu", "nam", "sau" }
                    : new[] { "nhat", "nhi", "ba", "tu", "nam", "sau" };
            var prizeNames = draw.Prizes.Select(prize => NormalizeText(prize.Name)).ToList();
            return requiredPrizeTypes.All(type => prizeNames.Any(name => name.Contains(type, StringComparison.Ordinal)))
                && draw.Prizes.SelectMany(prize => prize.Numbers).Any(value => ThreeDigitNumber.IsMatch(value));
        }

        private static string NormalizeText(string value) =>
            (value ?? "").ToLowerInvariant().Replace('đ', 'd').Normalize(System.Text.NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                .Aggregate(new System.Text.StringBuilder(), (builder, character) => builder.Append(character)).ToString();

        private static bool InRangeUnique(List<string> values, int count, int min, int max)
        {
            if (values.Count != count) return false;
            var parsed = new List<int>();
            foreach (var value in values)
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
                    return false;
                parsed.Add(number);
            }
            return parsed.Distinct().Count() == count;
        }

        private static List<int> ParseNumbers(List<string> values) =>
            values.Select(value => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? (int?)number : null)
                .Where(value => value.HasValue).Select(value => value.Value).ToList();

        private static List<string> SplitValues(string value) =>
            string.IsNullOrWhiteSpace(value) ? new List<string>()
                : value.Split(new[] { ' ', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        private static List<string> SplitPrizeValues(string value) =>
            string.IsNullOrWhiteSpace(value) ? new List<string>()
                : value.Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string GameCodeFromSlug(string slug) =>
            StatisticsGameOrder.FirstOrDefault(game => LotteryUrls.Slug(game) == slug);

        private static string DisplayGameName(string game) => game == "MAX3D_PLUS" ? "Max 3D+"
            : game == "BINGO18" ? "Bingo18" : LotteryUrls.GameName(game);

        private static VietlottStatisticsToolViewModel[] Tools(params (string Slug, string Name, string Description)[] items) =>
            items.Select(item => new VietlottStatisticsToolViewModel { Slug = item.Slug, Name = item.Name, Description = item.Description }).ToArray();

        private static VietlottStatisticsToolViewModel[] Max3dTools() => Tools(
            ("tan-suat-bo-ba", "Tần suất bộ ba", "Số lần và số kỳ xuất hiện từng bộ ba được lưu đúng dạng ba chữ số."),
            ("hai-so-cuoi", "Hai số cuối", "Tần suất hai chữ số cuối của các bộ ba trong giải."),
            ("chu-so-theo-vi-tri", "Chữ số theo vị trí", "Tần suất chữ số ở hàng trăm, hàng chục và hàng đơn vị."),
            ("theo-giai", "Theo giải", "Số bộ ba và số kỳ ghi nhận theo đúng tên giải lưu trong dữ liệu."),
            ("theo-ky", "Theo kỳ", "Lịch sử các giải và bộ ba hợp lệ theo kỳ."),
            ("chua-xuat-hien", "Chưa xuất hiện", "Số kỳ kể từ lần xuất hiện của từng bộ ba trong phạm vi giải đã lưu."));

        private sealed class DrawLoadResult { public List<Draw> Draws { get; set; } = new List<Draw>(); }
        private sealed class Draw
        {
            public long Id { get; set; }
            public string GameCode { get; set; }
            public string DrawNo { get; set; }
            public DateTime DrawDate { get; set; }
            public List<string> Numbers { get; set; } = new List<string>();
            public List<string> SpecialNumbers { get; set; } = new List<string>();
            public int? Total { get; set; }
            public string Size { get; set; }
            public string OddEven { get; set; }
            public long? Jackpot1 { get; set; }
            public long? Jackpot2 { get; set; }
            public bool IsComplete { get; set; }
            public List<Prize> Prizes { get; set; } = new List<Prize>();
        }
        private sealed class Prize { public string Name { get; set; } public List<string> Numbers { get; set; } = new List<string>(); public string Value { get; set; } }
        private sealed class TripleObservation { public TripleObservation(Draw draw, string prizeName, string number) { Draw = draw; PrizeName = prizeName; Number = number; } public Draw Draw { get; } public string PrizeName { get; } public string Number { get; } }
    }
}
