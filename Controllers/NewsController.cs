using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using XoSoWeb.Models;
using XoSoWeb.Services;

namespace XoSoWeb.Controllers
{
    public sealed class NewsController : Controller
    {
        private readonly INewsArticleService news;
        private readonly SiteSettings site;
        public NewsController(INewsArticleService news, SiteSettings site)
        {
            this.news = news;
            this.site = site;
        }

        [HttpGet("/tin-tuc")]
        public Task<IActionResult> Index([FromQuery] int page = 1) => List(null, page);

        [HttpGet("/tin-tuc/vietlott")]
        public Task<IActionResult> Vietlott([FromQuery] int page = 1) => List(NewsArticleCategories.Vietlott, page);

        [HttpGet("/tin-tuc/trung-thuong-vietlott")]
        public Task<IActionResult> Winners([FromQuery] int page = 1) => List(NewsArticleCategories.Winners, page);

        [HttpGet("/tin-tuc/huong-dan")]
        public Task<IActionResult> Guides([FromQuery] int page = 1) => List(NewsArticleCategories.Guides, page);

        [HttpGet("/tin-tuc/bai-viet/{slug}")]
        public async Task<IActionResult> Article(string slug)
        {
            var article = await news.GetPublishedAsync(slug, HttpContext.RequestAborted);
            if (article == null)
            {
                var currentSlug = await news.ResolveSlugAsync(slug, HttpContext.RequestAborted);
                return currentSlug == null ? NotFound() : RedirectPermanent("/tin-tuc/bai-viet/" + currentSlug);
            }
            SetArticleMetadata(this, article, false, site);
            return View("~/Views/News/Detail.cshtml", new NewsArticleDetailViewModel { Article = article });
        }

        private async Task<IActionResult> List(string category, int page)
        {
            if (!ModelState.IsValid || page < 1 || page > 10000) return BadRequest();
            var model = await news.GetPageAsync(category, page, HttpContext.RequestAborted);
            if (page > model.PageCount) return NotFound();
            ViewData["Title"] = model.Title;
            ViewData["Description"] = "Tin tức, thông tin Vietlott và hướng dẫn tra cứu đã được biên tập.";
            if (category != null)
                ViewData["BreadcrumbAncestors"] = new[] { new BreadcrumbLink { Label = "Tin tức", Path = "/tin-tuc" } };
            if (model.Articles.Count == 0) ViewData["NoIndex"] = true;
            if (page > 1) ViewData["Title"] = model.Title + " · Trang " + page;
            ViewData["Description"] = category switch
            {
                NewsArticleCategories.Vietlott => "Tin tức Vietlott đã được biên tập, gồm thông tin trò chơi và các nội dung liên quan.",
                NewsArticleCategories.Winners => "Tin trao thưởng Vietlott với thông tin, ngày quay, ngày trao và nguồn được biên tập.",
                NewsArticleCategories.Guides => "Hướng dẫn tra cứu kết quả, sử dụng website và hiểu thông tin xổ số.",
                _ => "Tin tức xổ số và Vietlott được biên tập, có nguồn và thời điểm đăng rõ ràng."
            };
            return View("~/Views/News/Index.cshtml", model);
        }

        internal static void SetArticleMetadata(Controller controller, NewsArticle article, bool preview, SiteSettings site)
        {
            var canonicalPath = "/tin-tuc/bai-viet/" + article.Slug;
            controller.ViewData["Title"] = string.IsNullOrWhiteSpace(article.SeoTitle) ? article.Title : article.SeoTitle;
            controller.ViewData["Description"] = string.IsNullOrWhiteSpace(article.MetaDescription) ? article.Summary : article.MetaDescription;
            controller.ViewData["BreadcrumbAncestors"] = article.Category == NewsArticleCategories.General
                ? new[] { new BreadcrumbLink { Label = "Tin tức", Path = "/tin-tuc" } }
                : new[]
                {
                    new BreadcrumbLink { Label = "Tin tức", Path = "/tin-tuc" },
                    new BreadcrumbLink { Label = CategoryLabel(article.Category), Path = CategoryPath(article.Category) }
                };
            controller.ViewData["BreadcrumbCurrentLabel"] = article.Title;
            controller.ViewData["NoIndex"] = preview;
            controller.ViewData["OgType"] = "article";
            controller.ViewData["OgImage"] = article.FeaturedImageUrl;
            controller.ViewData["OgImageAlt"] = article.FeaturedImageAlt;
            controller.ViewData["ArticlePublishedAt"] = article.PublishedAt.HasValue ? NewsDateTime.IsoUtc(article.PublishedAt.Value) : null;
            controller.ViewData["ArticleModifiedAt"] = article.UpdatedAt == default ? null : NewsDateTime.IsoUtc(article.UpdatedAt);
            controller.ViewData["ArticleAuthor"] = article.Author;
            var schema = new System.Collections.Generic.Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = article.Category == NewsArticleCategories.Guides ? "Article" : "NewsArticle",
                ["headline"] = article.Title,
                ["description"] = string.IsNullOrWhiteSpace(article.MetaDescription) ? article.Summary : article.MetaDescription,
                ["mainEntityOfPage"] = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["@type"] = "WebPage",
                    ["@id"] = site.Absolute(canonicalPath)
                }
            };
            if (article.UpdatedAt != default) schema["dateModified"] = NewsDateTime.IsoUtc(article.UpdatedAt);
            if (!preview && article.PublishedAt.HasValue) schema["datePublished"] = NewsDateTime.IsoUtc(article.PublishedAt.Value);
            if (!string.IsNullOrWhiteSpace(article.Author))
                schema["author"] = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["@type"] = "Person",
                    ["name"] = article.Author
                };
            if (!string.IsNullOrWhiteSpace(article.FeaturedImageUrl)) schema["image"] = site.ImageUrl(article.FeaturedImageUrl);
            if (!string.IsNullOrWhiteSpace(site.OrganizationName))
                schema["publisher"] = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["@type"] = "Organization",
                    ["name"] = site.OrganizationName
                };
            controller.ViewData["ArticleStructuredJson"] = System.Text.Json.JsonSerializer.Serialize(schema);
        }

        internal static string CategoryPath(string category) => category switch
        {
            NewsArticleCategories.Winners => "/tin-tuc/trung-thuong-vietlott",
            NewsArticleCategories.Guides => "/tin-tuc/huong-dan",
            NewsArticleCategories.Vietlott => "/tin-tuc/vietlott",
            _ => "/tin-tuc"
        };

        internal static string CategoryLabel(string category) => category switch
        {
            NewsArticleCategories.Winners => "Tin trúng thưởng Vietlott",
            NewsArticleCategories.Guides => "Hướng dẫn tra cứu và sử dụng",
            NewsArticleCategories.Vietlott => "Tin Vietlott",
            _ => "Tin tức"
        };
    }
}
