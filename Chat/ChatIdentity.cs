using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace XoSoWeb.Chat
{
    public class ChatPerson
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string Color { get; set; }
    }

    public class ChatIdentity
    {
        private readonly IDataProtector protector;
        public ChatIdentity(IDataProtectionProvider provider) => protector = provider.CreateProtector("community-chat.guest.v1");
        public string Read(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(subject)) throw new HubException("Tài khoản chưa có định danh hợp lệ.");
                return "u-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
            }
            try
            {
                var value = protector.Unprotect(context.Request.Cookies["chat-guest"] ?? "").Split('|');
                if (value.Length == 2 && Guid.TryParseExact(value[0], "N", out _) && long.Parse(value[1]) > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    return "g-" + value[0];
            }
            catch { /* Invalid or expired credentials never become a caller supplied identity. */ }
            return null;
        }
        public string Issue(HttpContext context)
        {
            var id = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append("chat-guest", protector.Protect(id + "|" + DateTimeOffset.UtcNow.AddDays(365).ToUnixTimeSeconds()),
                new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromDays(365), IsEssential = true, Path = "/" });
            return "g-" + id;
        }
        public static string Validate(string value, bool name)
        {
            value = (value ?? "").Normalize(NormalizationForm.FormC).Trim();
            if (value.Length < (name ? 2 : 1) || value.Length > (name ? 40 : 500)
                || Regex.IsMatch(value, @"[<>\p{Cf}]|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")
                || (name && (Regex.IsMatch(value, @"[\r\n\t]") || Regex.IsMatch(value, @"admin|administrator|moderator|quản\s*trị|điều\s*hành|hỗ\s*trợ|system", RegexOptions.IgnoreCase))))
                throw new HubException(name ? "Tên phải có 2–40 ký tự, không chứa HTML hoặc danh xưng quản trị." : "Tin nhắn phải có 1–500 ký tự văn bản, không chứa HTML.");
            return value;
        }
    }
}
