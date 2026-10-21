using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;

namespace XoSoWeb.Services
{
    public static class NewsContentSanitizer
    {
        private static readonly HashSet<string> AllowedTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "blockquote", "br", "code", "div", "em", "figcaption", "figure",
            "h2", "h3", "h4", "hr", "img", "li", "ol", "p", "pre", "s", "strong",
            "table", "tbody", "td", "th", "thead", "tr", "u", "ul"
        };
        private static readonly HashSet<string> VoidTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "br", "hr", "img"
        };
        private static readonly HashSet<string> DropContentTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "iframe", "object", "script", "style", "template"
        };

        public static string SanitizeHtml(string content)
        {
            var parser = new HtmlParser();
            var document = parser.ParseDocument(string.Empty);
            var nodes = parser.ParseFragment(content ?? string.Empty, document.Body);
            var output = new StringBuilder();
            foreach (var node in nodes) AppendNode(output, node);
            return output.ToString();
        }

        private static void AppendNode(StringBuilder output, INode node)
        {
            if (node is IText text)
            {
                output.Append(HtmlEncoder.Default.Encode(text.Data));
                return;
            }
            if (!(node is IElement element)) return;

            var tag = element.LocalName.ToLowerInvariant();
            if (DropContentTags.Contains(tag)) return;
            if (!AllowedTags.Contains(tag))
            {
                foreach (var child in element.ChildNodes) AppendNode(output, child);
                return;
            }
            if (tag == "img" && !IsAllowedUri(element.GetAttribute("src"), allowLocalImage: true)) return;

            output.Append('<').Append(tag);
            foreach (var attribute in element.Attributes)
            {
                var name = attribute.Name.ToLowerInvariant();
                if (name == "alt" || name == "title")
                    AppendAttribute(output, name, attribute.Value);
                else if (name == "href" && tag == "a" && IsAllowedUri(attribute.Value, allowLocalImage: false))
                    AppendAttribute(output, name, attribute.Value);
                else if (name == "src" && tag == "img" && IsAllowedUri(attribute.Value, allowLocalImage: true))
                    AppendAttribute(output, name, attribute.Value);
                else if ((name == "width" || name == "height")
                    && tag == "img" && int.TryParse(attribute.Value, out var dimension)
                    && dimension is > 0 and <= 4096)
                    AppendAttribute(output, name, dimension.ToString());
                else if (name == "scope" && tag == "th"
                    && new[] { "row", "col", "rowgroup", "colgroup" }.Contains(attribute.Value, StringComparer.OrdinalIgnoreCase))
                    AppendAttribute(output, name, attribute.Value.ToLowerInvariant());
            }
            output.Append('>');
            if (VoidTags.Contains(tag)) return;
            foreach (var child in element.ChildNodes) AppendNode(output, child);
            output.Append("</").Append(tag).Append('>');
        }

        private static void AppendAttribute(StringBuilder output, string name, string value) =>
            output.Append(' ').Append(name).Append("=\"").Append(HtmlEncoder.Default.Encode(value)).Append('"');

        private static bool IsAllowedUri(string value, bool allowLocalImage)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\\', '\r', '\n', '\0' }) >= 0)
                return false;
            if (allowLocalImage)
            {
                var decoded = Uri.UnescapeDataString(value);
                if (decoded.StartsWith("/images/news/", StringComparison.Ordinal)
                    && !decoded.StartsWith("//", StringComparison.Ordinal)
                    && !decoded.Contains('?') && !decoded.Contains('#')
                    && !decoded.Contains('\\')
                    && !decoded.Split('/').Any(part => part == "." || part == "..")
                    && new[] { ".jpg", ".jpeg", ".png", ".webp" }
                        .Contains(System.IO.Path.GetExtension(decoded), StringComparer.OrdinalIgnoreCase))
                    return true;
            }
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443
                || !string.IsNullOrEmpty(uri.UserInfo) || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return false;
            if (IPAddress.TryParse(uri.Host, out var address)
                && (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal))
                return false;
            return true;
        }
    }
}
