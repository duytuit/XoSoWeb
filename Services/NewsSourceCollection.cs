using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public sealed class NewsCollectionOptions
    {
        public bool AutomaticEnabled { get; set; } = true;
        public int MorningHour { get; set; } = 8;
        public int EveningHour { get; set; } = 20;
    }

    public sealed class VietlottNewsSourceStatus
    {
        public bool CanCollect { get; set; }
        public string Message { get; set; }
        public string SourceUrl { get; set; }
        public string Title { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public interface IVietlottNewsSourceAdapter
    {
        Task<VietlottNewsSourceStatus> CheckAsync(CancellationToken cancellationToken);
    }

    public sealed class VietlottNewsSourceAdapter : IVietlottNewsSourceAdapter
    {
        private const string ListingUrl = "https://vietlott.vn/vi/tin-tuc/tin-trung-thuong";
        private static readonly Regex PublishedDatePattern =
            new Regex(@"(?<!\d)\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}", RegexOptions.Compiled);
        private static readonly TimeZoneInfo VietNamTimeZone = GetVietNamTimeZone();
        private readonly HttpClient httpClient;

        public VietlottNewsSourceAdapter(HttpClient httpClient) => this.httpClient = httpClient;

        public async Task<VietlottNewsSourceStatus> CheckAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ListingUrl);
            request.Headers.UserAgent.ParseAdd("XoSoWeb-NewsCollector/1.0");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Vietlott news listing returned HTTP {(int)response.StatusCode}.");
            if (response.RequestMessage?.RequestUri == null
                || response.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(response.RequestMessage.RequestUri.Host, "vietlott.vn", StringComparison.OrdinalIgnoreCase)
                || response.RequestMessage.RequestUri.AbsolutePath != "/vi/tin-tuc/tin-trung-thuong")
                throw new HttpRequestException("Vietlott news listing redirected outside the configured source URL.");
            if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
                throw new InvalidDataException("Vietlott news listing exceeded the 2 MB response limit.");

            var html = await ReadBoundedHtml(response, cancellationToken);
            var document = new HtmlParser().ParseDocument(html);
            var articles = document.QuerySelectorAll("a[href]")
                .Select(anchor => ParseListingItem(anchor))
                .Where(item => item != null)
                .OrderByDescending(item => item.PublishedAt)
                .ToList();
            var latest = articles.FirstOrDefault();
            if (latest == null)
                return new VietlottNewsSourceStatus
                {
                    CanCollect = false,
                    SourceUrl = ListingUrl,
                    Message = "Không tìm thấy bài trúng thưởng mới nhất theo cấu trúc trang đã xác minh; chưa tạo bản nháp."
                };

            return new VietlottNewsSourceStatus
            {
                CanCollect = true,
                SourceUrl = latest.Url,
                Title = latest.Title,
                PublishedAt = latest.PublishedAt,
                Message = "Đã đọc tiêu đề, ngày đăng và đường dẫn bài mới nhất từ trang danh sách Vietlott."
            };
        }

        private static ListingItem ParseListingItem(IElement anchor)
        {
            if (!Uri.TryCreate(new Uri(ListingUrl), anchor.GetAttribute("href"), out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443
                || !string.Equals(uri.Host, "vietlott.vn", StringComparison.OrdinalIgnoreCase)
                || !uri.AbsolutePath.StartsWith("/vi/tin-tuc/tin-trung-thuong/", StringComparison.Ordinal))
                return null;

            var title = WebUtility.HtmlDecode(anchor.TextContent).Trim();
            if (title.Length < 3 || title.Length > 240) return null;

            var ancestor = anchor.ParentElement;
            for (var depth = 0; ancestor != null && depth < 8; ancestor = ancestor.ParentElement, depth++)
            {
                var match = PublishedDatePattern.Match(ancestor.TextContent);
                if (!match.Success) continue;
                if (!DateTime.TryParseExact(match.Value, "dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"),
                        DateTimeStyles.None, out var localDate))
                    return null;
                var utcDate = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), VietNamTimeZone);
                return new ListingItem(title, uri.GetLeftPart(UriPartial.Path), utcDate);
            }
            return null;
        }

        private static async Task<string> ReadBoundedHtml(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                if (memory.Length + count > 2 * 1024 * 1024)
                    throw new InvalidDataException("Vietlott news listing exceeded the 2 MB response limit.");
                await memory.WriteAsync(buffer, 0, count, cancellationToken);
            }

            var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
            Encoding encoding;
            try
            {
                encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Vietlott news listing declared an unsupported character encoding.", exception);
            }
            return encoding.GetString(memory.ToArray());
        }

        private static TimeZoneInfo GetVietNamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }

        private sealed class ListingItem
        {
            public ListingItem(string title, string url, DateTime publishedAt)
            {
                Title = title;
                Url = url;
                PublishedAt = publishedAt;
            }
            public string Title { get; }
            public string Url { get; }
            public DateTime PublishedAt { get; }
        }
    }

    public sealed class NewsCollectionService
    {
        private static readonly SemaphoreSlim LocalGate = new SemaphoreSlim(1, 1);
        private readonly XoSoDBContext db;
        private readonly IVietlottNewsSourceAdapter adapter;
        private readonly ILogger<NewsCollectionService> logger;

        public NewsCollectionService(XoSoDBContext db, IVietlottNewsSourceAdapter adapter, ILogger<NewsCollectionService> logger)
        {
            this.db = db;
            this.adapter = adapter;
            this.logger = logger;
        }

        public async Task<NewsCollectionRun> RunAsync(string triggeredBy, CancellationToken cancellationToken)
        {
            if (!await LocalGate.WaitAsync(0, cancellationToken))
                return new NewsCollectionRun { Status = "Skipped", Message = "Một lượt thu thập khác đang chạy trên instance này." };

            try
            {
                var started = DateTime.UtcNow;
                var source = await adapter.CheckAsync(cancellationToken);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                if (!await AcquireApplicationLock(transaction, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new NewsCollectionRun { Status = "Skipped", Message = "Một instance khác đang thu thập tin." };
                }

                var run = new NewsCollectionRun
                {
                    StartedAt = started,
                    CompletedAt = DateTime.UtcNow,
                    TriggeredBy = triggeredBy,
                    FoundCount = source.CanCollect ? 1 : 0,
                    Message = source.Message
                };

                if (!source.CanCollect)
                {
                    run.Status = "Blocked";
                }
                else
                {
                    var normalizedUrl = NewsSourcePolicy.NormalizeEditorialUrl(source.SourceUrl);
                    if (normalizedUrl == null || !NewsSourcePolicy.TryNormalizeVietlottUrl(normalizedUrl, out _))
                        throw new InvalidDataException("The collected article URL is outside the Vietlott HTTPS source allowlist.");
                    var sourceHash = NewsSourcePolicy.UrlHash(normalizedUrl);
                    var duplicate = await db.NewsArticles.AnyAsync(article =>
                        article.NormalizedSourceUrlHash == sourceHash, cancellationToken);
                    if (duplicate)
                    {
                        run.Status = "Duplicate";
                        run.DuplicateCount = 1;
                        run.Message = "Bài mới nhất đã có trong hệ thống; không tạo bản nháp trùng.";
                    }
                    else
                    {
                        var title = source.Title.Trim();
                        var slug = await BuildUniqueSlug(title, sourceHash, cancellationToken);
                        var publishedAt = source.PublishedAt ?? started;
                        var publishedAtVietnam = publishedAt.AddHours(7);
                        var article = new NewsArticle
                        {
                            Title = title,
                            Slug = slug,
                            Summary = $"Vietlott đăng tin trúng thưởng ngày {publishedAtVietnam:dd/MM/yyyy}. Xem thông tin đầy đủ tại bài viết nguồn.",
                            Content = $"<p>Vietlott đăng thông tin về: {System.Net.WebUtility.HtmlEncode(title)}.</p><p>Bài nguồn được đăng ngày {publishedAtVietnam:dd/MM/yyyy HH:mm} (giờ Việt Nam). Vui lòng xem chi tiết tại <a href=\"{System.Net.WebUtility.HtmlEncode(source.SourceUrl)}\" rel=\"noopener noreferrer\">nguồn Vietlott</a>.</p>",
                            Category = NewsArticleCategories.Vietlott,
                            Author = "Vietlott",
                            Editor = "news-collector",
                            SourceName = "Vietlott",
                            SourceUrl = normalizedUrl,
                            NormalizedSourceUrl = normalizedUrl,
                            NormalizedSourceUrlHash = sourceHash,
                            SourcePublishedAt = publishedAt,
                            CollectedAt = started,
                            CreatedAt = started,
                            UpdatedAt = started,
                            Status = NewsArticleStatuses.Draft
                        };
                        db.NewsArticles.Add(article);
                        await db.SaveChangesAsync(cancellationToken);
                        db.NewsArticleRevisions.Add(new NewsArticleRevision
                        {
                            ArticleId = article.Id,
                            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(article),
                            ChangedBy = "news-collector",
                            ChangeType = "collected-headline",
                            CreatedAt = started
                        });
                        run.Status = "Created";
                        run.NewDraftCount = 1;
                        run.Message = "Đã tạo bản nháp từ tiêu đề, ngày đăng và liên kết nguồn; cần biên tập kiểm tra trước khi xuất bản.";
                    }
                }

                db.NewsCollectionRuns.Add(run);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Vietlott news collection completed: status={Status}, found={Found}, new={New}, duplicates={Duplicates}. {Message}",
                    run.Status, run.FoundCount, run.NewDraftCount, run.DuplicateCount, run.Message);
                return run;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Vietlott news collection failed.");
                throw;
            }
            finally
            {
                LocalGate.Release();
            }
        }

        private async Task<bool> AcquireApplicationLock(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
            CancellationToken cancellationToken)
        {
            var command = db.Database.GetDbConnection().CreateCommand();
            await using (command)
            {
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = N'XoSoWeb.NewsCollection.Vietlott', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 0; SELECT @result;";
                var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                return result >= 0;
            }
        }

        private async Task<string> BuildUniqueSlug(string title, string sourceHash, CancellationToken cancellationToken)
        {
            var baseSlug = LotteryUrls.Slug(title);
            if (baseSlug.Length > 160) baseSlug = baseSlug.Substring(0, 160).TrimEnd('-');
            var slug = baseSlug;
            var suffix = "-" + sourceHash.Substring(0, 12).ToLowerInvariant();
            if (await db.NewsArticles.AnyAsync(article => article.Slug == slug, cancellationToken))
                slug = baseSlug.Substring(0, Math.Min(baseSlug.Length, 160 - suffix.Length)) + suffix;
            return slug.Length < 2 ? "vietlott-tin-trung-thuong" + suffix : slug;
        }
    }

    public sealed class NewsCollectionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory scopes;
        private readonly IOptions<NewsCollectionOptions> options;
        private readonly ILogger<NewsCollectionWorker> logger;

        public NewsCollectionWorker(IServiceScopeFactory scopes, IOptions<NewsCollectionOptions> options, ILogger<NewsCollectionWorker> logger)
        {
            this.scopes = scopes;
            this.options = options;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!options.Value.AutomaticEnabled)
            {
                logger.LogInformation("Scheduled Vietlott news collection is disabled.");
                return;
            }
            var morningHour = Math.Clamp(options.Value.MorningHour, 0, 23);
            var eveningHour = Math.Clamp(options.Value.EveningHour, 0, 23);
            if (morningHour == eveningHour)
                throw new InvalidOperationException("NewsCollection morning and evening hours must be different.");

            var timeZone = GetVietNamTimeZone();
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                var nextRun = GetNextRun(now, morningHour, eveningHour);
                var delay = nextRun - now;
                logger.LogInformation("Next Vietlott news collection scheduled at {ScheduledAt} Vietnam time.", nextRun);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NewsCollectionService>()
                        .RunAsync("scheduler", stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Scheduled news collection failed; the next scheduled time will retry.");
                }
            }
        }

        private static DateTimeOffset GetNextRun(DateTimeOffset now, int morningHour, int eveningHour)
        {
            var todaysRuns = new[]
            {
                new DateTimeOffset(now.Year, now.Month, now.Day, morningHour, 0, 0, now.Offset),
                new DateTimeOffset(now.Year, now.Month, now.Day, eveningHour, 0, 0, now.Offset)
            }.OrderBy(run => run).ToArray();
            return todaysRuns.FirstOrDefault(run => now < run, todaysRuns[0].AddDays(1));
        }

        private static TimeZoneInfo GetVietNamTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }
    }
}
