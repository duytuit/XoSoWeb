using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace XoSoWeb.Services
{
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
    var title = (viewData["Title"]?.ToString() ?? Site.SiteName) + " | " + Site.SiteName;
    var descriptions = new Dictionary<string, string> {
        ["/"] = "Tra cứu kết quả xổ số Miền Bắc, Miền Trung, Miền Nam và Vietlott theo ngày quay, cùng thống kê từ dữ liệu đã lưu.",
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
    var filterKeys = new[] { "day", "gameCode", "offset", "endDate", "region", "week" };
    var filters = request.Query.Where(x => filterKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
    var canonicalPath = path + (filters.Count == 0 ? "" : "?" + string.Join("&", filters.Select(x => Uri.EscapeDataString(filterKeys.First(k => k.Equals(x.Key, StringComparison.OrdinalIgnoreCase))) + "=" + Uri.EscapeDataString(x.Value.ToString()))));
    var canonical = Site.Absolute(canonicalPath);
    var noindex = !Site.AllowIndexing || filters.Count > 0 || (bool?)viewData["NoIndex"] == true;
    var graph = new List<object> { new Dictionary<string, object> { ["@type"] = "WebSite", ["@id"] = Site.Absolute("/#website"), ["url"] = Site.Absolute("/"), ["name"] = Site.SiteName, ["inLanguage"] = "vi" } };
    if (!string.IsNullOrWhiteSpace(Site.OrganizationName))
    {
        var organization = new Dictionary<string, object> { ["@type"] = "Organization", ["@id"] = Site.Absolute("/#organization"), ["name"] = Site.OrganizationName, ["url"] = Site.Absolute("/") };
        if (!string.IsNullOrWhiteSpace(Site.ContactEmail)) organization["email"] = Site.ContactEmail;
        if (!string.IsNullOrWhiteSpace(Site.ContactPhone)) organization["telephone"] = Site.ContactPhone;
        graph.Add(organization);
    }
    var crumbs = new List<object> { new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = 1, ["name"] = "Trang chủ", ["item"] = Site.Absolute("/") } };
    var parentPath = viewData["ParentPath"]?.ToString();
    var parentLabel = viewData["ParentLabel"]?.ToString();
    if (path != "/")
    {
        if (parentPath != null && parentPath != "/" && parentPath != path)
            crumbs.Add(new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = crumbs.Count + 1, ["name"] = parentLabel, ["item"] = Site.Absolute(parentPath) });
        crumbs.Add(new Dictionary<string, object> { ["@type"] = "ListItem", ["position"] = crumbs.Count + 1, ["name"] = viewData["Title"]?.ToString() ?? Site.SiteName, ["item"] = canonical });
        graph.Add(new Dictionary<string, object> { ["@type"] = "BreadcrumbList", ["itemListElement"] = crumbs });
    }
            return new SeoMetadata { Title = title, Description = description, Canonical = canonical, NoIndex = noindex,
                StructuredJson = JsonSerializer.Serialize(new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = graph }) };
        }
    }
}
