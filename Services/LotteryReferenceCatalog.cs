using System;
using System.Collections.Generic;
using System.Linq;
using XoSoWeb.Models;

namespace XoSoWeb.Services
{
    public static class LotteryReferenceCatalog
    {
        private static readonly DateTime CheckedOn = new DateTime(2026, 10, 21);
        private static readonly string[] Weekdays =
        {
            "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"
        };

        private static readonly List<LotteryReferenceGameLink> VietlottGames = new List<LotteryReferenceGameLink>
        {
            new LotteryReferenceGameLink { Name = "Mega 6/45", Slug = "mega645" },
            new LotteryReferenceGameLink { Name = "Power 6/55", Slug = "power655" },
            new LotteryReferenceGameLink { Name = "Lotto 5/35", Slug = "lotto535" },
            new LotteryReferenceGameLink { Name = "Keno", Slug = "keno" },
            new LotteryReferenceGameLink { Name = "Max 3D", Slug = "max3d" },
            new LotteryReferenceGameLink { Name = "Max 3D+", Slug = "max3d-plus" },
            new LotteryReferenceGameLink { Name = "Max 3D Pro", Slug = "max3d-pro" },
            new LotteryReferenceGameLink { Name = "Bingo18", Slug = "bingo18" }
        };

        public static bool TryBuildSchedule(
            string region, int? weekday, string station, string game, out LotteryReferencePageViewModel model)
        {
            var regionInfo = GetRegion(region);
            var isVietlott = region == "vietlott";
            if (regionInfo == null && !isVietlott)
            {
                model = null;
                return false;
            }
            if (weekday.HasValue && (weekday < 1 || weekday > 7))
            {
                model = null;
                return false;
            }

            var selectedGame = isVietlott && !string.IsNullOrWhiteSpace(game)
                ? VietlottGames.FirstOrDefault(item => item.Slug == game)
                : null;
            if (isVietlott && !string.IsNullOrWhiteSpace(game) && selectedGame == null)
            {
                model = null;
                return false;
            }

            var scopeName = isVietlott ? (selectedGame?.Name ?? "Vietlott") : regionInfo.Name;
            var path = isVietlott ? "/lich-quay/vietlott" : "/lich-quay/" + region;
            var sources = isVietlott
                ? GetVietlottSources(selectedGame?.Slug)
                : GetRegionalSources(region);
            var scheduleRows = new List<LotteryReferenceScheduleRow>();
            if (station != null || weekday.HasValue || selectedGame != null)
                scheduleRows = scheduleRows.Where(row =>
                    (station == null || string.Equals(row.Station, station, StringComparison.OrdinalIgnoreCase))
                    && (!weekday.HasValue || row.Weekday == weekday)
                    && (selectedGame == null || row.RegionOrGame == selectedGame.Name)).ToList();

            model = new LotteryReferencePageViewModel
            {
                PageKind = "schedule",
                Title = isVietlott ? "Lịch quay " + scopeName : "Lịch quay Miền " + scopeName,
                ScopeName = scopeName,
                ScopePath = path,
                ParentPath = "/lich-quay",
                ParentLabel = "Lịch quay",
                Description = "Lịch mở thưởng chính thức chưa được xác minh trong nguồn dữ liệu hiện có. Không dùng ngày kết quả lưu trong website để suy ra lịch quay.",
                Today = GetVietnamDate(),
                WeekdayFilter = weekday,
                StationFilter = station,
                GameFilter = selectedGame?.Slug,
                MissingInformation = "Chưa có lịch phát hành chính thức được xác minh cho phạm vi này. Giờ bắt đầu quay, giờ hoàn tất kết quả, tần suất và ngoại lệ theo ngày chưa được công bố tại đây.",
                LastChecked = CheckedOn,
                ScheduleRows = scheduleRows,
                ScheduleExceptions = new List<LotteryReferenceScheduleException>(),
                Sources = sources,
                GameLinks = isVietlott ? VietlottGames : new List<LotteryReferenceGameLink>(),
                Stations = regionInfo == null ? new List<string>() : StationCatalog.Stations
                    .Where(item => item.Region == region).Select(item => item.Name).Distinct().OrderBy(name => name).ToList()
            };
            return true;
        }

        public static bool TryBuildPrizeStructure(string region, string game, out LotteryReferencePageViewModel model)
        {
            var isVietlott = region == "vietlott";
            var regionInfo = isVietlott ? null : GetRegion(region);
            var selectedGame = isVietlott
                ? VietlottGames.FirstOrDefault(item => item.Slug == game)
                : null;
            if ((!isVietlott && regionInfo == null) || (isVietlott && selectedGame == null))
            {
                model = null;
                return false;
            }

            var scopeName = isVietlott ? selectedGame.Name : regionInfo.Name;
            var path = isVietlott ? "/co-cau-giai-thuong/vietlott/" + game : "/co-cau-giai-thuong/" + region;
            model = new LotteryReferencePageViewModel
            {
                PageKind = "prize",
                Title = "Cơ cấu giải thưởng " + (isVietlott ? scopeName : "Miền " + scopeName),
                ScopeName = scopeName,
                ScopePath = path,
                ParentPath = "/co-cau-giai-thuong",
                ParentLabel = "Cơ cấu giải thưởng",
                GameFilter = selectedGame?.Slug,
                Description = "Chưa có bảng cơ cấu giải thưởng và văn bản hiệu lực được xác minh để công bố cho phạm vi này.",
                Today = GetVietnamDate(),
                MissingInformation = "Chưa xác minh được số lượng giải, số chữ số cần khớp, giá trị giải, điều kiện trúng, đơn vị áp dụng và ngày hiệu lực. Không sử dụng giá trị giải ghi nhận ở một kỳ làm cơ cấu giải cố định.",
                LastChecked = CheckedOn,
                EffectiveFrom = null,
                EffectiveTo = null,
                TicketPrice = null,
                TicketUnit = null,
                PrizeRows = new List<LotteryReferencePrizeRow>(),
                PrizeVersions = new List<LotteryPrizeStructureVersion>(),
                Sources = isVietlott ? GetVietlottSources(game) : GetRegionalSources(region),
                GameLinks = isVietlott ? VietlottGames : new List<LotteryReferenceGameLink>()
            };
            return true;
        }

        public static LotteryReferencePageViewModel BuildIndex(bool schedule)
        {
            var links = new List<LotteryReferenceGameLink>
            {
                new LotteryReferenceGameLink { Name = "Miền Bắc", Slug = "mien-bac" },
                new LotteryReferenceGameLink { Name = "Miền Trung", Slug = "mien-trung" },
                new LotteryReferenceGameLink { Name = "Miền Nam", Slug = "mien-nam" },
                new LotteryReferenceGameLink { Name = "Vietlott", Slug = "vietlott" }
            };
            return new LotteryReferencePageViewModel
            {
                PageKind = schedule ? "schedule-index" : "prize-index",
                Title = schedule ? "Lịch quay xổ số" : "Cơ cấu giải thưởng",
                ScopeName = schedule ? "Lịch quay" : "Cơ cấu giải thưởng",
                ScopePath = schedule ? "/lich-quay" : "/co-cau-giai-thuong",
                Description = schedule
                    ? "Tra cứu lịch quay theo miền và trò chơi. Thông tin chưa được xác minh được đánh dấu rõ."
                    : "Tra cứu cơ cấu giải thưởng theo miền và trò chơi Vietlott. Chỉ công bố mức giải khi có nguồn chính thức xác minh.",
                MissingInformation = schedule
                    ? "Chưa có lịch quay chính thức được xác minh để công bố."
                    : "Chưa có cơ cấu giải thưởng được xác minh để công bố.",
                Today = GetVietnamDate(),
                LastChecked = CheckedOn,
                GameLinks = links,
                Sources = new List<LotteryReferenceSource>()
            };
        }

        public static string WeekdayName(int weekday) =>
            weekday >= 1 && weekday <= 7 ? Weekdays[weekday % 7] : "Chưa xác minh";

        private static DateTime GetVietnamDate()
        {
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).Date;
        }

        private static RegionInfo GetRegion(string region) => region switch
        {
            "mien-bac" => new RegionInfo { Code = "MB", Name = "Bắc", ResultPath = "/xo-so-mien-bac" },
            "mien-trung" => new RegionInfo { Code = "MT", Name = "Trung", ResultPath = "/xo-so-mien-trung" },
            "mien-nam" => new RegionInfo { Code = "MN", Name = "Nam", ResultPath = "/xo-so-mien-nam" },
            _ => null
        };

        private static List<LotteryReferenceSource> GetRegionalSources(string region)
        {
            if (region == "mien-nam")
                return new List<LotteryReferenceSource>
                {
                    new LotteryReferenceSource
                    {
                        Label = "Công ty TNHH MTV Xổ số kiến thiết TP. Hồ Chí Minh",
                        Url = "https://www.xskthcm.com/",
                        Status = "Đã kiểm tra trang đơn vị phát hành; trang được đọc không xác nhận giờ quay hay văn bản cơ cấu giải."
                    }
                };
            return new List<LotteryReferenceSource>();
        }

        private static List<LotteryReferenceSource> GetVietlottSources(string game)
        {
            var url = game == "mega645" ? "https://vietlott.vn/vi/choi/mega-6-45"
                : game == "power655" ? "https://vietlott.vn/vi/choi/power-6-55"
                : "https://vietlott.vn/vi/home";
            return new List<LotteryReferenceSource>
            {
                new LotteryReferenceSource
                {
                    Label = "Công ty Xổ số điện toán Việt Nam (Vietlott)",
                    Url = url,
                    Status = "Đã kiểm tra ngày 21/10/2026; nội dung thể lệ/lịch tương ứng không tải được đầy đủ để xác minh thông tin chi tiết."
                }
            };
        }

        private sealed class RegionInfo
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string ResultPath { get; set; }
        }
    }
}
