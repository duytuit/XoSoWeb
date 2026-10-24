using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace XoSoWeb.Services
{
    public sealed class SiteSettings
    {
        private string configuredSiteUrl;

        public SiteSettings() { }
        public SiteSettings(IHttpContextAccessor httpContextAccessor)
        {
            // Retained for constructor compatibility. SEO origins never trust request headers.
        }

        public string SiteUrl
        {
            get
            {
                return configuredSiteUrl;
            }
            set => configuredSiteUrl = value;
        }
        public string SiteName { get; set; } = "Xem Nhanh";
        public string Slogan { get; set; } = "Kết quả rõ ràng — tra cứu dễ dàng";
        public string LogoUrl { get; set; } = "/images/logo.png";
        public string FaviconUrl { get; set; } = "/images/favicon.ico";
        public string DefaultShareImage { get; set; } = "/images/Biểu tượng mắt xanh lưới cam.png";
        public string OrganizationName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string ContactAddress { get; set; }
        public string DataSourceDescription { get; set; }
        public string VerificationProcess { get; set; }
        public string SearchConsoleVerification { get; set; }
        public string AnalyticsId { get; set; }
        // Compatibility with deployments that already configure this setting.
        public string ShareImagePath { get => DefaultShareImage; set => DefaultShareImage = value; }
        public string LiveSocketUrl { get; set; }
        public string StagingUsername { get; set; }
        public string StagingPassword { get; set; }
        public bool AllowIndexing { get; set; } = true;

        public void Validate(bool development)
        {
            if (string.IsNullOrWhiteSpace(configuredSiteUrl))
                throw new InvalidOperationException("Site:SiteUrl is required. Configure the active origin explicitly for each environment.");
            if (!string.IsNullOrWhiteSpace(configuredSiteUrl))
            {
                if (!Uri.TryCreate(configuredSiteUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "https" && !(development && uri.Scheme == "http"))
                    || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query)
                    || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)
                    || (!development && (uri.IsLoopback || !uri.Host.Contains('.') || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))))
                    throw new InvalidOperationException("Site:SiteUrl must be a public HTTPS origin in production (no path, credentials, query or fragment).");
                SiteUrl = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }
            if (string.IsNullOrWhiteSpace(SiteName)) throw new InvalidOperationException("Site:SiteName is required.");
            if (!string.IsNullOrEmpty(AnalyticsId) && !Regex.IsMatch(AnalyticsId, @"\AG-[A-Z0-9]+\z"))
                throw new InvalidOperationException("Site:AnalyticsId must be a GA4 G- identifier.");
            if (!string.IsNullOrEmpty(LiveSocketUrl) && (!Uri.TryCreate(LiveSocketUrl, UriKind.Absolute, out var socket) || socket.Scheme != "wss"))
                throw new InvalidOperationException("Site:LiveSocketUrl must use wss.");
            foreach (var asset in new[] { LogoUrl, FaviconUrl, DefaultShareImage })
                if (!string.IsNullOrWhiteSpace(asset) && (!asset.StartsWith("/") || asset.StartsWith("//") || asset.Contains('\\')
                    || asset.Contains("..") || asset.Contains('?') || asset.Contains('#') || asset.Contains('%')))
                    throw new InvalidOperationException("Brand assets must use local absolute paths without traversal, query or fragment.");
        }

        public void ValidateAssets(string webRoot)
        {
            foreach (var asset in new[] { LogoUrl, FaviconUrl, DefaultShareImage })
                if (!string.IsNullOrWhiteSpace(asset) && !System.IO.File.Exists(System.IO.Path.Combine(webRoot, asset.TrimStart('/'))))
                    throw new InvalidOperationException("A configured brand asset does not exist in wwwroot: " + asset);
        }

        public string Absolute(string path)
        {
            if (string.IsNullOrWhiteSpace(SiteUrl)) throw new InvalidOperationException("Site:SiteUrl is required to generate public URLs.");
            return SiteUrl.TrimEnd('/') + (path.StartsWith("/") ? path : "/" + path);
        }
        public string ImageUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith("/") && !path.StartsWith("//")) return Absolute(string.Join("/", path.Split('/').Select(Uri.EscapeDataString)));
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)
                ? uri.AbsoluteUri : null;
        }
    }

    public static class LotteryUrls
    {
        public static DateTime Today => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
        public static bool TryDate(string value, out DateTime date) =>
            DateTime.TryParseExact(value, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && date >= new DateTime(1753, 1, 1) && date <= Today;
        public static string RegionCode(string value)
        {
            var key = Slug(value);
            if (key == "mb" || key.Contains("bac")) return "MB";
            if (key == "mt" || key.Contains("trung")) return "MT";
            if (key == "mn" || key.Contains("nam")) return "MN";
            return null;
        }
        public static string RegionPath(string code) => code == "MB" ? "/xo-so-mien-bac" : code == "MT" ? "/xo-so-mien-trung" : "/xo-so-mien-nam";
        public static string Slug(string value)
        {
            var text = (value ?? "").ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var ch in text)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
            return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-").Trim('-');
        }
        public static string ProvincePath(string name)
        {
            var slug = StationCatalog.Find(name)?.Slug ?? Slug(name);
            return "/" + (slug.StartsWith("xo-so-") ? slug : "xo-so-" + slug);
        }
        public static bool ValidDrawNo(string value) => value != null && Regex.IsMatch(value, @"\A[0-9]{1,30}\z");
        public static string GamePath(string code) => "/vietlott/" + Slug(code);
        public static string DrawPath(string code, string drawNo) => GamePath(code) + "/ky/" + Uri.EscapeDataString(drawNo);
        public static string GameName(string code) => code switch
        {
            "BINGO18" => "Bingo 18", "KENO" => "Keno", "LOTTO535" => "Lotto 5/35",
            "MAX3D" => "Max 3D", "MAX3D_PLUS" => "Max 3D+", "MAX3D_PRO" => "Max 3D Pro",
            "MEGA645" => "Mega 6/45", "POWER655" => "Power 6/55", _ => code
        };
    }
}
