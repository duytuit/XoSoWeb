using System;
using System.Globalization;

namespace XoSoWeb.Services
{
    public static class NewsDateTime
    {
        private static readonly TimeZoneInfo VietnamTimeZone = FindVietnamTimeZone();

        public static DateTime ToVietnam(DateTime utc) =>
            TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTimeZone);

        public static string FormatDate(DateTime utc) => ToVietnam(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        public static string FormatDateTime(DateTime utc) => ToVietnam(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        public static string IsoUtc(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

        private static TimeZoneInfo FindVietnamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }
    }
}
