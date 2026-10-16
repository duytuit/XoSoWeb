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

namespace XoSoWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly XoSoDBContext _context;
        private readonly RedisService _redisService;

        public HomeController(ILogger<HomeController> logger, RedisService redisService, XoSoDBContext context)
        {
            _logger = logger;
            _redisService = redisService;
            _context = context;
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
                Vietlott = await GetVietlottResultsAsync()
            };
            return View(model);
        }
        [HttpGet("/privacy")]
        public IActionResult Privacy()
        {
            return View("Privacy");
        }
        [HttpGet("/support")]
        public IActionResult Support()
        {
            return View("Support");
        }
        [HttpGet("/xo-so-mien-bac")]
        public async Task<IActionResult> XoSoMienBac([FromQuery] int? day = null)
        {
            return View("XoSoMienBac", await GetRegionResultsAsync("MB", "Miền Bắc", day, 7));
        }
        [HttpGet("/xo-so-mien-trung")]
        public async Task<IActionResult> XoSoMienTrung([FromQuery] int? day = null)
        {
            return View("XoSoMienTrung", await GetRegionResultsAsync("MT", "Miền Trung", day, 7));
        }
        [HttpGet("/xo-so-mien-nam")]
        public async Task<IActionResult> XoSoMienNam([FromQuery] int? day = null)
        {
            return View("XoSoMienNam", await GetRegionResultsAsync("MN", "Miền Nam", day, 7));
        }
        [HttpGet("/vietlott")]
        public async Task<IActionResult> Vietlott([FromQuery] string gameCode = null, [FromQuery] int offset = 0)
        {
            return View("Vietlott", await GetVietlottResultsAsync(gameCode, offset));
        }
        [HttpPost("support")]
        public async Task<IActionResult> Contact([FromBody] ContactSupportRequest request)
        {
            return Ok(new
            {
                success = true,
                message = "Support request received."
            });
        }

        private async Task<XoSoRegionViewModel> GetRegionResultsAsync(string region, string regionName, int? selectedDay = null, int resultDays = 1)
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
                    SELECT Id, Region, ProvinceCode, ProvinceName, DrawDate, Prizes,
                        DENSE_RANK() OVER (ORDER BY CAST(DrawDate AS date) DESC) AS DrawDateRank
                    FROM dbo.XoSoResults
                    WHERE (Region = @region OR Region = @storedRegionName)
                        AND (@selectedDay IS NULL OR
                            ((DATEDIFF(day, '19000101', DrawDate) % 7 + 7) % 7) + 1 = @selectedDay)
                )
                SELECT Id, Region, ProvinceCode, ProvinceName, DrawDate, Prizes
                FROM RankedResults
                WHERE DrawDateRank <= @resultDays
                ORDER BY DrawDate DESC, ProvinceName, Id DESC;";
            AddParameter(command, "@region", region);
            AddParameter(command, "@storedRegionName", storedRegionName);
            AddParameter(command, "@selectedDay", selectedDay);
            AddParameter(command, "@resultDays", resultDays);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                model.Results.Add(new XoSoResult
                {
                    Id = reader.GetInt64(0),
                    Region = reader.GetString(1),
                    ProvinceCode = reader.GetString(2),
                    ProvinceName = reader.GetString(3),
                    DrawDate = reader.GetDateTime(4),
                    Prizes = JsonSerializer.Deserialize<List<XoSoPrize>>(reader.GetString(5), JsonOptions) ?? new List<XoSoPrize>()
                });
            }
            await reader.CloseAsync();
            await connection.CloseAsync();
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

        private async Task<VietlottPageViewModel> GetVietlottResultsAsync(string selectedGameCode = null, int selectedOffset = 0)
        {
            var model = new VietlottPageViewModel();
            var supportedGames = new[] { "BINGO18", "KENO", "LOTTO535", "MAX3D", "MAX3D_PLUS", "MAX3D_PRO", "MEGA645", "POWER655" };
            if (!supportedGames.Contains(selectedGameCode))
            {
                selectedGameCode = null;
                selectedOffset = 0;
            }
            selectedOffset = Math.Max(0, selectedOffset);
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
                    WHERE GameCode IN
                    (
                        'BINGO18', 'KENO', 'LOTTO535', 'MAX3D',
                        'MAX3D_PLUS', 'MAX3D_PRO', 'MEGA645', 'POWER655'
                    )
                )
                SELECT Id, GameCode, DrawNo, DrawDate, Numbers, SpecialNumbers, EstimatedPrize, EstimatedPrize2, Total, OddEven, Size, TotalPeriods
                FROM LatestResults
                WHERE ResultRank = CASE
                    WHEN @selectedGameCode IS NOT NULL AND GameCode = @selectedGameCode THEN @selectedOffset + 1
                    ELSE 1
                END
                ORDER BY CASE GameCode
                    WHEN 'LOTTO535' THEN 1
                    WHEN 'MAX3D' THEN 2
                    WHEN 'MAX3D_PLUS' THEN 3
                    WHEN 'MAX3D_PRO' THEN 4
                    WHEN 'MEGA645' THEN 5
                    WHEN 'POWER655' THEN 6
                    WHEN 'BINGO18' THEN 7
                    WHEN 'KENO' THEN 8
                END;";
            AddParameter(command, "@selectedGameCode", selectedGameCode);
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
            foreach (var result in results)
            {
                result.Prizes = await GetVietlottPrizesAsync(connection, result.Id);
                model.Results.Add(result);
            }
            await connection.CloseAsync();

            return model;
        }

        private static async Task<List<VietlottPrize>> GetVietlottPrizesAsync(System.Data.Common.DbConnection connection, long resultId)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"SELECT PrizeName, Numbers, PrizeValue FROM dbo.VietlottPrizes WHERE ResultId = @resultId ORDER BY Id;";
            AddParameter(command, "@resultId", resultId);

            var prizes = new List<VietlottPrize>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                prizes.Add(new VietlottPrize
                {
                    PrizeName = reader.GetString(0),
                    Numbers = SplitValues(reader.IsDBNull(1) ? null : reader.GetString(1)),
                    Value = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }

            return prizes;
        }

        private static List<string> SplitValues(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
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
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Content { get; set; }
}
