using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public interface INewsArticleService
    {
        Task<System.Collections.Generic.List<NewsArticleListItem>> GetLatestAsync(int limit, CancellationToken cancellationToken);
        Task<NewsArticleListViewModel> GetPageAsync(string category, int page, CancellationToken cancellationToken);
        Task<NewsArticle> GetPublishedAsync(string slug, CancellationToken cancellationToken);
        Task<string> ResolveSlugAsync(string slug, CancellationToken cancellationToken);
        Task<System.Collections.Generic.List<NewsSitemapItem>> GetSitemapPageAsync(int offset, int limit, CancellationToken cancellationToken);
        Task<System.Collections.Generic.List<string>> GetPublishedCategoryPathsAsync(CancellationToken cancellationToken);
    }

    public sealed class NewsSitemapItem
    {
        public string Slug { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public sealed class NewsArticleService : INewsArticleService
    {
        private readonly XoSoDBContext db;
        public NewsArticleService(XoSoDBContext db) => this.db = db;

        public Task<System.Collections.Generic.List<NewsArticleListItem>> GetLatestAsync(int limit, CancellationToken cancellationToken)
        {
            limit = Math.Clamp(limit, 1, 5);
            var now = DateTime.UtcNow;
            return db.NewsArticles.AsNoTracking()
                .Where(article => article.Status == NewsArticleStatuses.Published && article.PublishedAt <= now)
                .OrderByDescending(article => article.PublishedAt).ThenByDescending(article => article.Id)
                .Take(limit)
                .Select(article => new NewsArticleListItem
                {
                    Id = article.Id,
                    Title = article.Title,
                    Slug = article.Slug,
                    Summary = article.Summary,
                    FeaturedImageUrl = article.FeaturedImageUrl,
                    ThumbnailImageUrl = article.ThumbnailImageUrl,
                    FeaturedImageAlt = article.FeaturedImageAlt,
                    Category = article.Category,
                    Author = article.Author,
                    PublishedAt = article.PublishedAt.Value,
                    UpdatedAt = article.UpdatedAt
                }).ToListAsync(cancellationToken);
        }

        public async Task<NewsArticleListViewModel> GetPageAsync(string category, int page, CancellationToken cancellationToken)
        {
            page = Math.Clamp(page, 1, 10000);
            const int pageSize = 10;
            var now = DateTime.UtcNow;
            var query = db.NewsArticles.AsNoTracking()
                .Where(article => article.Status == NewsArticleStatuses.Published && article.PublishedAt <= now);
            if (category == NewsArticleCategories.Vietlott)
                query = query.Where(article => article.Category == NewsArticleCategories.Vietlott || article.Category == NewsArticleCategories.Winners);
            else if (category != null)
                query = query.Where(article => article.Category == category);

            var count = await query.CountAsync(cancellationToken);
            var articles = await query.OrderByDescending(article => article.PublishedAt).ThenByDescending(article => article.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(article => new NewsArticleListItem
                {
                    Id = article.Id,
                    Title = article.Title,
                    Slug = article.Slug,
                    Summary = article.Summary,
                    FeaturedImageUrl = article.FeaturedImageUrl,
                    ThumbnailImageUrl = article.ThumbnailImageUrl,
                    FeaturedImageAlt = article.FeaturedImageAlt,
                    Category = article.Category,
                    Author = article.Author,
                    PublishedAt = article.PublishedAt.Value,
                    UpdatedAt = article.UpdatedAt
                }).ToListAsync(cancellationToken);
            var title = category switch
            {
                NewsArticleCategories.Vietlott => "Tin Vietlott",
                NewsArticleCategories.Winners => "Tin trúng thưởng Vietlott",
                NewsArticleCategories.Guides => "Hướng dẫn tra cứu và sử dụng",
                _ => "Tin tức"
            };
            return new NewsArticleListViewModel
            {
                Title = title,
                Category = category,
                Page = page,
                PageCount = Math.Max(1, (count + pageSize - 1) / pageSize),
                Articles = articles
            };
        }

        public Task<NewsArticle> GetPublishedAsync(string slug, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            return db.NewsArticles.AsNoTracking()
                .FirstOrDefaultAsync(article => article.Slug == slug
                    && article.Status == NewsArticleStatuses.Published && article.PublishedAt <= now, cancellationToken);
        }

        public async Task<string> ResolveSlugAsync(string slug, CancellationToken cancellationToken)
        {
            var redirect = await db.NewsSlugRedirects.AsNoTracking()
                .Where(item => item.OldSlug == slug)
                .Select(item => (long?)item.ArticleId).FirstOrDefaultAsync(cancellationToken);
            if (!redirect.HasValue) return null;
            return await db.NewsArticles.AsNoTracking()
                .Where(article => article.Id == redirect.Value && article.Status == NewsArticleStatuses.Published)
                .Select(article => article.Slug).FirstOrDefaultAsync(cancellationToken);
        }

        public Task<System.Collections.Generic.List<NewsSitemapItem>> GetSitemapPageAsync(int offset, int limit, CancellationToken cancellationToken)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 1000);
            var now = DateTime.UtcNow;
            return db.NewsArticles.AsNoTracking()
                .Where(article => article.Status == NewsArticleStatuses.Published && article.PublishedAt <= now)
                .OrderBy(article => article.Id).Skip(offset).Take(limit)
                .Select(article => new NewsSitemapItem { Slug = article.Slug, ModifiedAt = article.UpdatedAt })
                .ToListAsync(cancellationToken);
        }

        public async Task<System.Collections.Generic.List<string>> GetPublishedCategoryPathsAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var categories = await db.NewsArticles.AsNoTracking()
                .Where(article => article.Status == NewsArticleStatuses.Published && article.PublishedAt <= now)
                .Select(article => article.Category).Distinct().ToListAsync(cancellationToken);
            if (categories.Count == 0) return new System.Collections.Generic.List<string>();
            var paths = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { "/tin-tuc" };
            if (categories.Contains(NewsArticleCategories.Vietlott) || categories.Contains(NewsArticleCategories.Winners))
                paths.Add("/tin-tuc/vietlott");
            if (categories.Contains(NewsArticleCategories.Winners)) paths.Add("/tin-tuc/trung-thuong-vietlott");
            if (categories.Contains(NewsArticleCategories.Guides)) paths.Add("/tin-tuc/huong-dan");
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
        }
    }
}
