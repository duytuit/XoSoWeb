using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XoSoWeb.Services
{
    public static class NewsSourcePolicy
    {
        public static string NormalizeEditorialUrl(string value)
        {
            if (!TryGetPublicHttpsUri(value, out var uri)) return null;
            return BuildCanonicalUri(uri);
        }

        public static bool TryNormalizeVietlottUrl(string value, out string normalized)
        {
            normalized = null;
            if (!TryGetPublicHttpsUri(value, out var uri)
                || (uri.Host != "vietlott.vn" && uri.Host != "www.vietlott.vn")
                || uri.Port != 443) return false;
            normalized = BuildCanonicalUri(uri);
            return true;
        }

        public static string UrlHash(string normalizedUrl) =>
            normalizedUrl == null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl)));

        private static bool TryGetPublicHttpsUri(string value, out Uri uri)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.IsLoopback
                || !uri.Host.Contains('.')
                || Regex.IsMatch(uri.Host, @"\A(?:10\.|127\.|192\.168\.|169\.254\.|172\.(?:1[6-9]|2\d|3[01])\.)", RegexOptions.IgnoreCase))
                return false;
            return true;
        }

        private static string BuildCanonicalUri(Uri uri)
        {
            var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part =>
                {
                    var key = Uri.UnescapeDataString(part.Split('=', 2)[0]);
                    return !key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                        && !new[] { "gclid", "fbclid", "mc_cid", "mc_eid", "_ga" }.Contains(key, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(part => part.Split('=', 2)[0], StringComparer.Ordinal)
                .ThenBy(part => part, StringComparer.Ordinal);
            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty,
                Query = string.Join("&", query)
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }
    }
}
