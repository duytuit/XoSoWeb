using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace XoSoWeb.Services
{
    public sealed class BreadcrumbLink
    {
        public string Label { get; set; }
        public string Path { get; set; }
    }

    public sealed class SeoMetadata
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Canonical { get; set; }
        public bool NoIndex { get; set; }
        public string StructuredJson { get; set; }
        public static SeoMetadata Create(SiteSettings Site, HttpRequest request, ViewDataDictionary viewData)
        {

    var path = request.Path.Value ?? "/";
    var pageTitle = viewData["Title"]?.ToString();
    var title = path == "/" ? Site.SiteName + " - Kết quả xổ số hôm nay, XSMB, XSMT, XSMN, Vietlott"
        : string.IsNullOrWhiteSpace(pageTitle) ? Site.SiteName
        : pageTitle.Equals(Site.SiteName, StringComparison.OrdinalIgnoreCase) || pageTitle.EndsWith(" | " + Site.SiteName, StringComparison.OrdinalIgnoreCase)
            ? pageTitle : pageTitle + " | " + Site.SiteName;
    var descriptions = new Dictionary<string, string> {
        ["/"] = "Xem kết quả xổ số Bắc, Trung, Nam và Vietlott mới nhất. Tra cứu theo ngày, theo đài, xem thống kê và lịch quay trên giao diện gọn, dễ đọc.",
        ["/xo-so-mien-bac"] = "Kết quả xổ số Miền Bắc theo ngày quay, bảng giải và thống kê lô tô đầu đuôi.",
        ["/xo-so-mien-trung"] = "Kết quả xổ số Miền Trung theo tỉnh và ngày quay, bảng giải và lịch sử có dữ liệu.",
        ["/xo-so-mien-nam"] = "Kết quả xổ số Miền Nam theo tỉnh và ngày quay, bảng giải và lịch sử có dữ liệu.",
        ["/vietlott"] = "Tra cứu kết quả các trò chơi Vietlott, bộ số, mã kỳ quay và bảng giải theo dữ liệu hiện có.",
        ["/thong-ke"] = "Thống kê giải đặc biệt ba miền trong khoảng thời gian được chọn từ kết quả đã lưu.",
        ["/thong-ke-tan-suat-loto"] = "Thống kê tần suất lô tô trong tuần theo miền, từ các kết quả có trong hệ thống.",
        ["/privacy"] = "Thông tin chính sách bảo mật và cách xử lý thông tin khi sử dụng website.",
        ["/terms"] = "Điều khoản sử dụng website tra cứu kết quả xổ số và các giới hạn của thông tin tham khảo.",
        ["/support"] = "Liên hệ hỗ trợ, báo lỗi hoặc gửi yêu cầu sửa thông tin kết quả xổ số.",
        ["/lien-he-quang-cao"] = "Thông tin các hình thức hợp tác và liên hệ quảng cáo trên website." };
    var description = viewData["Description"]?.ToString() ?? (descriptions.TryGetValue(path, out var desc) ? desc : "Tra cứu " + viewData["Title"] + " và các kết quả liên quan.");
    var filterKeys = new[] { "days", "date", "length", "target", "reverse", "day", "gameCode", "offset", "endDate", "region", "week", "periods", "endDraw", "prize", "sort", "q", "page", "station", "weekday", "game" };
    var filters = request.Query.Where(x => filterKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
    var canonicalPath = path + (filters.Count == 0 ? "" : "?" + string.Join("&", filters.Select(x => Uri.EscapeDataString(filterKeys.First(k => k.Equals(x.Key, StringComparison.OrdinalIgnoreCase))) + "=" + Uri.EscapeDataString(x.Value.ToString()))));
    var canonical = Site.Absolute(canonicalPath);
    var editorialPagination = path.StartsWith("/tin-tuc", StringComparison.Ordinal) && filters.All(f => f.Key.Equals("page", StringComparison.OrdinalIgnoreCase));
    var noindex = !Site.AllowIndexing || (filters.Count > 0 && !editorialPagination) || (bool?)viewData["NoIndex"] == true;
    var graph = new List<object> { new Dictionary<string, object> { ["@type"] = "WebSite", ["@id"] = Site.Absolute("/#website"), ["url"] = Site.Absolute("/"), ["name"] = Site.SiteName, ["inLanguage"] = "vi" } };
    if (!string.IsNullOrWhiteSpace(Site.OrganizationName))
    {
        var organization = new Dictionary<string, object> { ["@type"] = "Organization", ["@id"] = Site.Absolute("/#organization"), ["name"] = Site.OrganizationName, ["url"] = Site.Absolute("/") };
        if (!string.IsNullOrWhiteSpace(Site.ContactEmail)) organization["email"] = Site.ContactEmail;
        if (!string.IsNullOrWhiteSpace(Site.ContactPhone)) organization["telephone"] = Site.ContactPhone;
        if (!string.IsNullOrWhiteSpace(Site.LogoUrl)) organization["logo"] = Site.Absolute(Site.LogoUrl);
        graph.Add(organization);
    }
    var crumbs = new List<object> { new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = 1, ["name"] = "Trang chủ", ["item"] = Site.Absolute("/") } };
    var ancestors = viewData["BreadcrumbAncestors"] as IEnumerable<BreadcrumbLink>;
    if (ancestors != null)
    {
        foreach (var ancestor in ancestors.Where(item => !string.IsNullOrWhiteSpace(item.Label)
            && !string.IsNullOrWhiteSpace(item.Path) && item.Path != "/"
            && !string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
            crumbs.Add(new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = crumbs.Count + 1, ["name"] = ancestor.Label, ["item"] = Site.Absolute(ancestor.Path) });
    }
    else if (viewData["ParentPath"] is string parentPath && parentPath != "/"
        && !string.Equals(parentPath, path, StringComparison.OrdinalIgnoreCase))
    {
        crumbs.Add(new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = crumbs.Count + 1, ["name"] = viewData["ParentLabel"]?.ToString(), ["item"] = Site.Absolute(parentPath) });
    }
    if (path != "/")
    {
        crumbs.Add(new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = crumbs.Count + 1, ["name"] = viewData["BreadcrumbCurrentLabel"]?.ToString() ?? viewData["Title"]?.ToString() ?? Site.SiteName, ["item"] = canonical });
        graph.Add(new Dictionary<string, object> { ["@type"] = "BreadcrumbList", ["itemListElement"] = crumbs });
    }
            if (viewData["ArticleStructuredJson"] is string articleStructuredJson)
            {
                using var articleDocument = JsonDocument.Parse(articleStructuredJson);
                graph.Add(articleDocument.RootElement.Clone());
            }
            return new SeoMetadata { Title = title, Description = description, Canonical = canonical, NoIndex = noindex,
                StructuredJson = JsonSerializer.Serialize(new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = graph }) };
        }
    }
}
