using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Linq;

namespace XoSoWeb.Services
{
    public static class NewsContentSanitizer
    {
        public static string ToSafeHtml(string content)
        {
            var lines = Regex.Split(WebUtility.HtmlDecode(content ?? string.Empty), @"\r\n|\r|\n");
            return string.Join("<br />", lines.Select(line => HtmlEncoder.Default.Encode(line)));
        }
    }
}
