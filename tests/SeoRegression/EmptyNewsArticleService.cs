using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XoSoWeb.Models;
using XoSoWeb.Services;

public sealed class EmptyNewsArticleService : INewsArticleService
{
    public Task<List<NewsArticleListItem>> GetLatestAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult(new List<NewsArticleListItem>());

    public Task<NewsArticleListViewModel> GetPageAsync(string category, int page, CancellationToken cancellationToken) =>
        Task.FromResult(new NewsArticleListViewModel
        {
            Category = category,
            Page = page,
            PageCount = 1,
            Title = category switch
            {
                NewsArticleCategories.Vietlott => "Tin Vietlott",
                NewsArticleCategories.Winners => "Tin trúng thưởng Vietlott",
                NewsArticleCategories.Guides => "Hướng dẫn tra cứu và sử dụng",
                _ => "Tin tức"
            }
        });

    public Task<NewsArticle> GetPublishedAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult<NewsArticle>(null);

    public Task<string> ResolveSlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult<string>(null);

    public Task<List<NewsSitemapItem>> GetSitemapPageAsync(int offset, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(new List<NewsSitemapItem>());

    public Task<List<string>> GetPublishedCategoryPathsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new List<string>());
}
