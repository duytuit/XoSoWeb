using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using XoSoWeb.Shares.BaseRepository;
using XoSoWeb.Shares;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;
using System.ComponentModel.DataAnnotations;

namespace XoSoWeb.Controllers
{
    public partial class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly XoSoDBContext _context;
        private readonly INewsArticleService _news;

        public HomeController(ILogger<HomeController> logger, XoSoDBContext context, INewsArticleService news)
        {
            _logger = logger;
            _context = context;
            _news = news;
        }

        /// <summary>
        /// List users
        /// </summary>
        /// <returns></returns>
        //[HttpGet]
        //public string Get([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        //{
        //return "Chào mừng bạn đến với chúng tôi....";
        //var value = _redisService.GetAsync("jtec_hn_database_update_AsyncKTNQ");
        //return value.Result ?? "No value found in Redis";
        //}
        [HttpGet("/")]
        public async Task<IActionResult> Index()
        {
            ViewData["Message"] = "Chào mừng đến Web API + View1";
            var model = new HomePageViewModel
            {
                MienBac = await GetRegionResultsAsync("MB", "Miền Bắc"),
                MienTrung = await GetRegionResultsAsync("MT", "Miền Trung"),
                MienNam = await GetRegionResultsAsync("MN", "Miền Nam"),
                Vietlott = await GetVietlottResultsAsync(),
                LatestNews = await _news.GetLatestAsync(5, HttpContext.RequestAborted)
            };
            return View(model);
        }
        [HttpGet("/lien-he-quang-cao")]
        public IActionResult About()
        {
            return View("About");
        }
        [HttpGet("/thong-ke")]
        public async Task<IActionResult> Statistics([FromQuery] DateTime? endDate = null)
        {
            if (!ModelState.IsValid || endDate > LotteryUrls.Today || endDate < new DateTime(1753, 1, 30)) return BadRequest();
            return View("Statistics", await GetSpecialPrizeStatisticsAsync(endDate));
        }
        [HttpGet("/privacy")]
        public IActionResult Privacy()
        {
            return View("Privacy");
        }

        [HttpGet("/thong-ke-tan-suat-loto")]
        public async Task<IActionResult> LotoFrequency([FromQuery] string region = "MB", [FromQuery] DateTime? week = null)
        {
            if (!ModelState.IsValid || week > LotteryUrls.Today || week < new DateTime(1753, 1, 8)) return BadRequest();
            region = (region ?? "MB").ToUpperInvariant();
            if (region != "MB" && region != "MT" && region != "MN") return BadRequest();
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            var date = week?.Date ?? today;
            if (date > today || date < new DateTime(1753, 1, 8)) date = today;
            var model = new LotoFrequencyViewModel
            {
                Region = region,
                WeekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7))
            };
            await _context.Database.OpenConnectionAsync();
            try
            {
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"SELECT Region, DrawDate, Prizes FROM dbo.XoSoResults
                    WHERE DrawDate >= @start AND DrawDate < @end
                    AND Region IN (@region, @storedRegion, @unaccentedRegion, @mixedRegion)
                    ORDER BY DrawDate, ProvinceName;";
                AddParameter(command, "@start", model.WeekStart);
                AddParameter(command, "@end", model.WeekStart.AddDays(7) < today.AddDays(1) ? model.WeekStart.AddDays(7) : today.AddDays(1));
                AddParameter(command, "@region", region);
                AddParameter(command, "@storedRegion", region == "MB" ? "Miền bắc" : region == "MT" ? "Miền trung" : "Miền nam");
                AddParameter(command, "@unaccentedRegion", region == "MB" ? "Mien Bac" : region == "MT" ? "Mien Trung" : "Mien Nam");
                AddParameter(command, "@mixedRegion", region == "MB" ? "Miền bac" : region == "MT" ? "Miền Trung" : "Miền Nam");
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (GetRegionCode(reader.GetString(0)) != region) continue;
                    var prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(2), JsonOptions)
                        ?? new List<XoSoPrize>();
                    model.Add(reader.GetDateTime(1), prizes);
                }
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
            return View("LotoFrequency", model);
        }

        [HttpGet("/terms")]
        public IActionResult Terms()
        {
            return View("Terms");
        }
        [HttpGet("/support")]
        public IActionResult Support()
        {
            return View("Support");
        }
        [HttpGet("/xo-so-mien-bac")]
        public async Task<IActionResult> XoSoMienBac([FromQuery] int? day = null)
        {
            if (!ModelState.IsValid || day < 1 || day > 7) return BadRequest();
            return View("XoSoMienBac", await GetRegionResultsAsync("MB", "Miền Bắc", day, 7));
        }
        [HttpGet("/xo-so-mien-trung")]
        public async Task<IActionResult> XoSoMienTrung([FromQuery] int? day = null)
        {
            if (!ModelState.IsValid || day < 1 || day > 7) return BadRequest();
            return View("XoSoMienTrung", await GetRegionResultsAsync("MT", "Miền Trung", day, 7));
        }
        [HttpGet("/xo-so-mien-nam")]
        public async Task<IActionResult> XoSoMienNam([FromQuery] int? day = null)
        {
            if (!ModelState.IsValid || day < 1 || day > 7) return BadRequest();
            return View("XoSoMienNam", await GetRegionResultsAsync("MN", "Miền Nam", day, 7));
        }
        [HttpGet("/vietlott")]
        public async Task<IActionResult> Vietlott([FromQuery] string gameCode = null, [FromQuery] int offset = 0)
        {
            if (!ModelState.IsValid || offset < 0 || offset > 99999 || (offset > 0 && gameCode == null)) return BadRequest();
            if (gameCode != null && !ArchiveCatalog.SupportedGames.Contains(gameCode)) return NotFound();
            if ((gameCode == "KENO" || gameCode == "BINGO18") && offset % 3 != 0) return BadRequest();
            if (gameCode != null && offset == 0) return RedirectPermanent(LotteryUrls.GamePath(gameCode));
            var model = await GetVietlottResultsAsync(gameCode, offset);
            if (offset > 0 && model.Results.Count == 0) return NotFound();
            return View("Vietlott", model);
        }
        [HttpPost("support")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact([FromBody] ContactSupportRequest request, [FromServices] TelegramContactService telegram)
        {
            if (request == null || !ModelState.IsValid)
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ thông tin hợp lệ." });

            if (!await telegram.SendAsync(request, HttpContext.RequestAborted))
                return StatusCode(502, new { success = false, message = "Chưa gửi được yêu cầu. Vui lòng thử lại sau." });

            return Ok(new
            {
                success = true,
                message = "Yêu cầu liên hệ đã được gửi thành công."
            });
        }

        private async Task<SpecialPrizeStatisticsViewModel> GetSpecialPrizeStatisticsAsync(DateTime? selectedEndDate)
        {
            var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            var endDate = selectedEndDate?.Date ?? today;
            if (endDate > today) endDate = today;
            if (endDate < new DateTime(1753, 1, 30)) endDate = today;
            var startDate = endDate.AddDays(-29);
            var model = new SpecialPrizeStatisticsViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                Regions = new List<SpecialPrizeRegionStatistics>
                {
                    new SpecialPrizeRegionStatistics { RegionCode = "MB", RegionName = "Miền Bắc" },
                    new SpecialPrizeRegionStatistics { RegionCode = "MT", RegionName = "Miền Trung" },
                    new SpecialPrizeRegionStatistics { RegionCode = "MN", RegionName = "Miền Nam" }
                }
            };

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Region, ProvinceName, DrawDate, Prizes
                FROM dbo.XoSoResults
                WHERE DrawDate >= @startDate
                    AND DrawDate < @endDateExclusive
                    AND Region IN
                    (
                        N'MB', N'Miền bắc', N'Miền Bắc', N'Miền bac', N'Mien Bac',
                        N'MT', N'Miền trung', N'Miền Trung', N'Mien Trung',
                        N'MN', N'Miền nam', N'Miền Nam', N'Mien Nam'
                    )
                ORDER BY DrawDate DESC, ProvinceName;";
            AddParameter(command, "@startDate", startDate);
            AddParameter(command, "@endDateExclusive", endDate.AddDays(1));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var regionCode = GetRegionCode(reader.GetString(0));
                var region = model.Regions.FirstOrDefault(item => item.RegionCode == regionCode);
                if (region == null)
                {
                    continue;
                }

                var prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(3), JsonOptions)
                    ?? new List<XoSoPrize>();
                var specialPrize = prizes.FirstOrDefault(IsSpecialPrize);
                if (specialPrize == null)
                {
                    continue;
                }

                region.Entries.Add(new SpecialPrizeStatisticsEntry
                {
                    DrawDate = reader.GetDateTime(2),
                    ProvinceName = reader.GetString(1),
                    Numbers = (specialPrize.Numbers ?? new List<string>())
                        .Where(number => !string.IsNullOrWhiteSpace(number)).ToList()
                });
            }

            await reader.CloseAsync();
            await connection.CloseAsync();
            return model;
        }

        private static string GetRegionCode(string region)
        {
            var normalizedRegion = (region ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedRegion == "mb" || normalizedRegion.Contains("bac") || normalizedRegion.Contains("bắc"))
            {
                return "MB";
            }
            if (normalizedRegion == "mt" || normalizedRegion.Contains("trung"))
            {
                return "MT";
            }
            if (normalizedRegion == "mn" || normalizedRegion.Contains("nam"))
            {
                return "MN";
            }

            return null;
        }

        private static bool IsSpecialPrize(XoSoPrize prize)
        {
            var name = (prize.PrizeName ?? string.Empty).Trim();
            return name.Contains("đặc biệt", StringComparison.OrdinalIgnoreCase)
                || name.Contains("dac biet", StringComparison.OrdinalIgnoreCase)
                || name.Equals("ĐB", StringComparison.OrdinalIgnoreCase)
                || name.Equals("DB", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Giải ĐB", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Giai DB", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<XoSoRegionViewModel> GetRegionResultsAsync(string region, string regionName, int? selectedDay = null, int resultDays = 1, DateTime? exactDate = null, string provinceName = null)
        {
            selectedDay = selectedDay >= 1 && selectedDay <= 7 ? selectedDay : null;
            var model = new XoSoRegionViewModel { Region = region, RegionName = regionName, SelectedDay = selectedDay };
            var storedRegionName = region switch
            {
                "MN" => "Miền nam",
                "MT" => "Miền trung",
                _ => "Miền bắc"
            };
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH RankedResults AS
                (
                    SELECT *,
                        DENSE_RANK() OVER (ORDER BY CAST(DrawDate AS date) DESC) AS DrawDateRank
                    FROM dbo.XoSoResults
                    WHERE (Region = @region OR Region = @storedRegionName OR Region = @unaccentedRegion OR Region = @mixedRegion)
                        AND DrawDate < @tomorrow
                        AND (@exactDate IS NULL OR (DrawDate >= @exactDate AND DrawDate < @dateEnd))
                        AND (@provinceName IS NULL OR ProvinceName = @provinceName OR (" + StationCatalog.IdentitySql + @") = @stationId)
                        AND (@selectedDay IS NULL OR
                            ((DATEDIFF(day, '19000101', DrawDate) % 7 + 7) % 7) + 1 = @selectedDay)
                )
                SELECT Id, Region, ProvinceCode, ProvinceName, DrawDate, Prizes, UpdatedAt, RankedResults.*
                FROM RankedResults
                WHERE DrawDateRank <= @resultDays
                ORDER BY RankedResults.DrawDate DESC, RankedResults.ProvinceName, RankedResults.Id DESC;";
            AddParameter(command, "@region", region);
            AddParameter(command, "@unaccentedRegion", region == "MB" ? "Mien Bac" : region == "MT" ? "Mien Trung" : "Mien Nam");
            AddParameter(command, "@mixedRegion", region == "MB" ? "Miền bac" : storedRegionName);
            AddParameter(command, "@tomorrow", LotteryUrls.Today.AddDays(1));
            AddParameter(command, "@exactDate", exactDate);
            AddParameter(command, "@dateEnd", exactDate?.AddDays(1));
            AddParameter(command, "@provinceName", provinceName);
            AddParameter(command, "@stationId", StationCatalog.Find(provinceName)?.Id);
            AddParameter(command, "@storedRegionName", storedRegionName);
            AddParameter(command, "@selectedDay", selectedDay);
            AddParameter(command, "@resultDays", resultDays);

            await using var reader = await command.ExecuteReaderAsync();
            var sourceNameOrdinal = Enumerable.Range(0, reader.FieldCount).FirstOrDefault(i => reader.GetName(i) == "SourceName", -1);
            var sourcePriorityOrdinal = Enumerable.Range(0, reader.FieldCount).FirstOrDefault(i => reader.GetName(i) == "SourcePriority", -1);
            while (await reader.ReadAsync())
            {
                model.Results.Add(new XoSoResult
                {
                    Id = reader.GetInt64(0),
                    SourceName = sourceNameOrdinal < 0 || reader.IsDBNull(sourceNameOrdinal) ? "legacy-unknown" : reader.GetString(sourceNameOrdinal),
                    SourcePriority = sourcePriorityOrdinal < 0 || reader.IsDBNull(sourcePriorityOrdinal) ? 0 : reader.GetInt32(sourcePriorityOrdinal),
                    UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Region = reader.GetString(1),
                    ProvinceCode = reader.GetString(2),
                    ProvinceName = reader.GetString(3),
                    DrawDate = reader.GetDateTime(4),
                    Prizes = ArchiveCatalog.ReadPrizes(reader.GetString(5))
                });
            }
            await reader.CloseAsync();
            await connection.CloseAsync();
            model.Results = StationCatalog.Merge(model.Results.Where(r => r.Prizes.Any(p => p.Numbers.Any(n => !string.IsNullOrWhiteSpace(n)))));
            // Incomplete recent records must not hide a valid older landing page advertised in the sitemap.
            if (model.Results.Count == 0 && !exactDate.HasValue && HttpContext?.RequestServices.GetService(typeof(ArchiveCatalog)) is ArchiveCatalog catalog)
            {
                var available = await catalog.GetAsync(HttpContext.RequestAborted);
                var latest = available.Where(x => x.Region == region && x.Date.HasValue && x.GameCode == null
                    && (provinceName == null ? x.ProvinceCode == null : x.ProvinceName == provinceName)
                    && (!selectedDay.HasValue || ((int)x.Date.Value.DayOfWeek + 6) % 7 + 1 == selectedDay))
                    .OrderByDescending(x => x.Date).FirstOrDefault();
                if (latest != null) return await GetRegionResultsAsync(region, regionName, selectedDay, 1, latest.Date, provinceName);
            }
            foreach (var result in model.Results)
            {
                result.LotoStatistics = BuildLotoStatistics(new List<XoSoResult> { result });
            }

            model.LotoStatistics = BuildLotoStatistics(model.Results);

            return model;
        }

        private static List<LotoStatisticRow> BuildLotoStatistics(List<XoSoResult> results)
        {
            var headNumbers = Enumerable.Range(0, 10).ToDictionary(digit => digit, _ => new SortedSet<int>());
            var tailNumbers = Enumerable.Range(0, 10).ToDictionary(digit => digit, _ => new SortedSet<int>());

            foreach (var result in results)
            {
                foreach (var prize in result.Prizes)
                {
                    foreach (var number in prize.Numbers ?? new List<string>())
                    {
                        var digits = new string(number.Where(char.IsDigit).ToArray());
                        if (digits.Length < 2)
                        {
                            continue;
                        }

                        var loto = digits.Substring(digits.Length - 2);
                        var headDigit = loto[0] - '0';
                        var tailDigit = loto[1] - '0';

                        headNumbers[headDigit].Add(tailDigit);
                        tailNumbers[tailDigit].Add(headDigit);
                    }
                }
            }

            return Enumerable.Range(0, 10)
                .Select(digit => new LotoStatisticRow
                {
                    Digit = digit,
                    HeadNumbers = string.Join(",", headNumbers[digit]),
                    TailNumbers = string.Join(",", tailNumbers[digit])
                })
                .ToList();
        }

        private async Task<VietlottPageViewModel> GetVietlottResultsAsync(string selectedGameCode = null, int selectedOffset = 0, string exactDraw = null)
        {
            var model = new VietlottPageViewModel();
            var supportedGames = new[] { "BINGO18", "KENO", "LOTTO535", "MAX3D", "MAX3D_PLUS", "MAX3D_PRO", "MEGA645", "POWER655" };
            if (!supportedGames.Contains(selectedGameCode))
            {
                selectedGameCode = null;
                selectedOffset = 0;
            }
            selectedOffset = Math.Max(0, selectedOffset);
            if (selectedGameCode == "BINGO18" || selectedGameCode == "KENO")
            {
                selectedOffset = selectedOffset / 3 * 3;
            }
            model.SelectedGameCode = selectedGameCode;
            model.SelectedOffset = selectedOffset;
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH LatestResults AS
                (
                    SELECT
                        Id,
                        GameCode,
                        DrawNo,
                        DrawDate,
                        Numbers,
                        SpecialNumbers,
                        EstimatedPrize,
                        EstimatedPrize2,
                        Total,
                        OddEven,
                        Size,
                        COUNT(*) OVER (PARTITION BY GameCode) AS TotalPeriods,
                        ROW_NUMBER() OVER (PARTITION BY GameCode ORDER BY DrawDate DESC, Id DESC) AS ResultRank
                    FROM dbo.VietlottResults
                    WHERE DrawDate < @tomorrow AND (@selectedGameCode IS NULL OR GameCode = @selectedGameCode)
                    AND (@exactDraw IS NULL OR DrawNo = @exactDraw) AND GameCode IN
                    (
                        'BINGO18', 'KENO', 'LOTTO535', 'MAX3D',
                        'MAX3D_PLUS', 'MAX3D_PRO', 'MEGA645', 'POWER655'
                    )
                )
                SELECT Id, GameCode, DrawNo, DrawDate, Numbers, SpecialNumbers, EstimatedPrize, EstimatedPrize2, Total, OddEven, Size, TotalPeriods
                FROM LatestResults
                WHERE
                    (
                        @selectedGameCode IS NULL
                        AND
                        (
                            (GameCode IN ('BINGO18', 'KENO') AND ResultRank <= 3)
                            OR (GameCode NOT IN ('BINGO18', 'KENO') AND ResultRank = 1)
                        )
                    )
                    OR
                    (
                        @selectedGameCode IS NOT NULL
                        AND GameCode = @selectedGameCode
                        AND ResultRank BETWEEN @selectedOffset + 1
                            AND @selectedOffset + CASE WHEN GameCode IN ('BINGO18', 'KENO') THEN 3 ELSE 1 END
                    )
                ORDER BY CASE GameCode
                    WHEN 'LOTTO535' THEN 1
                    WHEN 'MAX3D' THEN 2
                    WHEN 'MAX3D_PLUS' THEN 3
                    WHEN 'MAX3D_PRO' THEN 4
                    WHEN 'MEGA645' THEN 5
                    WHEN 'POWER655' THEN 6
                    WHEN 'BINGO18' THEN 7
                    WHEN 'KENO' THEN 8
                END, ResultRank;";
            AddParameter(command, "@selectedGameCode", selectedGameCode);
            AddParameter(command, "@exactDraw", exactDraw);
            AddParameter(command, "@tomorrow", LotteryUrls.Today.AddDays(1));
            AddParameter(command, "@selectedOffset", selectedOffset);

            var results = new List<VietlottResult>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var result = new VietlottResult
                {
                    Id = reader.GetInt64(0),
                    GameCode = reader.GetString(1),
                    DrawNo = reader.GetString(2),
                    DrawDate = reader.GetDateTime(3),
                    Numbers = SplitValues(reader.IsDBNull(4) ? null : reader.GetString(4)),
                    SpecialNumbers = SplitValues(reader.IsDBNull(5) ? null : reader.GetString(5)),
                    EstimatedPrize = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    EstimatedPrize2 = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                    Total = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    OddEven = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Size = reader.IsDBNull(10) ? null : reader.GetString(10),
                    TotalPeriods = reader.GetInt32(11),
                    Prizes = new List<VietlottPrize>()
                };
                results.Add(result);
            }

            await reader.CloseAsync();
            var prizesByResult = await GetVietlottPrizesAsync(connection, results.Select(x => x.Id).ToList());
            foreach (var result in results)
            {
                result.Prizes = prizesByResult.TryGetValue(result.Id, out var prizes) ? prizes : new List<VietlottPrize>();
                if (ArchiveCatalog.HasVietlottContent(result.GameCode, result.Numbers, result.Prizes.Any(p => p.Numbers.Any(n => !string.IsNullOrWhiteSpace(n) && n.All(c => c >= '0' && c <= '9')))))
                    model.Results.Add(result);
            }
            await connection.CloseAsync();

            return model;
        }

        private static async Task<Dictionary<long, List<VietlottPrize>>> GetVietlottPrizesAsync(System.Data.Common.DbConnection connection, List<long> resultIds)
        {
            var prizes = new Dictionary<long, List<VietlottPrize>>();
            if (resultIds.Count == 0) return prizes;
            await using var command = connection.CreateCommand();
            var names = resultIds.Select((id, index) => "@id" + index).ToList();
            command.CommandText = "SELECT ResultId, PrizeName, Numbers, PrizeValue FROM dbo.VietlottPrizes WHERE ResultId IN (" + string.Join(",", names) + ") ORDER BY ResultId, Id;";
            for (var index = 0; index < resultIds.Count; index++) AddParameter(command, names[index], resultIds[index]);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                if (!prizes.TryGetValue(id, out var rows)) prizes[id] = rows = new List<VietlottPrize>();
                rows.Add(new VietlottPrize
                {
                    PrizeName = reader.GetString(1),
                    Numbers = SplitValues(reader.IsDBNull(2) ? null : reader.GetString(2)),
                    Value = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }

            return prizes;
        }

        private static List<string> SplitValues(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(new[] { ' ', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

    }
}
public class ContactSupportRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; }
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; }
    [Required, StringLength(200)]
    public string Subject { get; set; }
    [Required, StringLength(3000)]
    public string Content { get; set; }
}
