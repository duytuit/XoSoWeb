using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XoSoWeb.Models;
using XoSoWeb.Services;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Controllers
{
    public sealed class NewsAdminOptions
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(PasswordHash);
    }

    public sealed class NewsLoginViewModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string ReturnUrl { get; set; }
        public string Error { get; set; }
    }

    [Route("quan-tri/tin-tuc")]
    public sealed class NewsAdminController : Controller
    {
        private static readonly HashSet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
        {
            NewsArticleCategories.General, NewsArticleCategories.Vietlott,
            NewsArticleCategories.Winners, NewsArticleCategories.Guides
        };
        private static readonly HashSet<string> TaxTreatments = new HashSet<string>(StringComparer.Ordinal)
        {
            "not-stated", "before-tax", "after-tax", "unknown"
        };
        private static readonly HashSet<string> Games = new HashSet<string>(ArchiveCatalog.SupportedGames, StringComparer.Ordinal);
        private readonly XoSoDBContext db;
        private readonly NewsAdminOptions admin;
        private readonly IMemoryCache cache;
        private readonly IWebHostEnvironment environment;
        private readonly NewsCollectionService collection;
        private readonly NewsSourceChangeTracker sourceTracker;
        private readonly ILogger<NewsAdminController> logger;
        private readonly SiteSettings site;

        public NewsAdminController(
            XoSoDBContext db,
            IOptions<NewsAdminOptions> admin,
            IMemoryCache cache,
            IWebHostEnvironment environment,
            NewsCollectionService collection,
            NewsSourceChangeTracker sourceTracker,
            SiteSettings site,
            ILogger<NewsAdminController> logger)
        {
            this.db = db;
            this.admin = admin.Value;
            this.cache = cache;
            this.environment = environment;
            this.collection = collection;
            this.sourceTracker = sourceTracker;
            this.site = site;
            this.logger = logger;
        }

        [AllowAnonymous, HttpGet("dang-nhap")]
        public IActionResult Login(string returnUrl = null) =>
            View("~/Views/NewsAdmin/Login.cshtml", new NewsLoginViewModel { ReturnUrl = returnUrl });

        [AllowAnonymous, HttpPost("dang-nhap"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(NewsLoginViewModel input)
        {
            if (!admin.IsConfigured)
            {
                input.Error = "Khu vực biên tập chưa được cấu hình tài khoản quản trị.";
                return View("~/Views/NewsAdmin/Login.cshtml", input);
            }

            var key = GetRateLimitKey();
            if (cache.TryGetValue<int>(key, out var attempts) && attempts >= 10)
            {
                input.Error = "Đăng nhập tạm khóa. Thử lại sau 15 phút.";
                return View("~/Views/NewsAdmin/Login.cshtml", input);
            }
            var valid = string.Equals(input.Username, admin.Username, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(input.Password)
                && BCrypt.Net.BCrypt.Verify(input.Password, admin.PasswordHash);
            if (!valid)
            {
                cache.Set(key, attempts + 1, TimeSpan.FromMinutes(15));
                input.Password = null;
                input.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
                logger.LogWarning("News editor login rejected.");
                return View("~/Views/NewsAdmin/Login.cshtml", input);
            }

            cache.Remove(key);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Username),
                new Claim(ClaimTypes.Name, admin.Username),
                new Claim(ClaimTypes.Role, "NewsAdmin")
            };
            await HttpContext.SignInAsync("NewsAdminCookie", new ClaimsPrincipal(new ClaimsIdentity(claims, "NewsAdminCookie")));
            return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl : "/quan-tri/tin-tuc");
        }

        [Authorize(Roles = "NewsAdmin"), HttpPost("dang-xuat"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("NewsAdminCookie");
            return Redirect("/tin-tuc");
        }

        [Authorize(Roles = "NewsAdmin"), HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var model = new NewsAdminListViewModel
            {
                Articles = await db.NewsArticles.AsNoTracking().OrderByDescending(article => article.UpdatedAt).Take(200).ToListAsync(HttpContext.RequestAborted),
                Runs = await db.NewsCollectionRuns.AsNoTracking().OrderByDescending(run => run.StartedAt).Take(20).ToListAsync(HttpContext.RequestAborted),
                SourceReviews = await db.NewsSourceRecords.AsNoTracking()
                    .Where(record => record.ReviewStatus != null && record.ReviewStatus != "Reviewed")
                    .OrderByDescending(record => record.ChangeDetectedAt ?? record.LastSeenAt).Take(100)
                    .ToListAsync(HttpContext.RequestAborted),
                CollectionStatus = TempData["CollectionStatus"]?.ToString()
            };
            return View("~/Views/NewsAdmin/Index.cshtml", model);
        }

        [Authorize(Roles = "NewsAdmin"), HttpGet("tao-moi")]
        public IActionResult Create() => View("~/Views/NewsAdmin/Edit.cshtml", new NewsArticleEditorViewModel
        {
            Category = NewsArticleCategories.Vietlott,
            TaxTreatment = "unknown"
        });

        [Authorize(Roles = "NewsAdmin"), HttpPost("tao-moi"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NewsArticleEditorViewModel input)
        {
            if (!ValidateArticle(input)) return View("~/Views/NewsAdmin/Edit.cshtml", input);
            var now = DateTime.UtcNow;
            var article = Map(input);
            article.CreatedAt = now;
            article.UpdatedAt = now;
            article.Status = NewsArticleStatuses.Draft;
            article.Editor = User.Identity.Name;
            article.NormalizedSourceUrl = NormalizeSourceUrl(input.SourceUrl);
            article.NormalizedSourceUrlHash = HashSourceUrl(article.NormalizedSourceUrl);
            if (!await ValidateUniqueSlug(article.Slug, null)) return View("~/Views/NewsAdmin/Edit.cshtml", input);
            if (!await ValidateUniqueSourceUrl(article.NormalizedSourceUrlHash, null)) return View("~/Views/NewsAdmin/Edit.cshtml", input);

            await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            db.NewsArticles.Add(article);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await AddRevision(article, "created");
            await transaction.CommitAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "NewsAdmin"), HttpGet("sua/{id:long}")]
        public async Task<IActionResult> Edit(long id)
        {
            var article = await db.NewsArticles.FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            return article == null ? NotFound() : View("~/Views/NewsAdmin/Edit.cshtml", Map(article));
        }

        [Authorize(Roles = "NewsAdmin"), HttpPost("sua/{id:long}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, NewsArticleEditorViewModel input)
        {
            if (id != input.Id) return BadRequest();
            var article = await db.NewsArticles.FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            if (article == null) return NotFound();
            if (!ValidateArticle(input)) return View("~/Views/NewsAdmin/Edit.cshtml", input);

            var oldSlug = article.Slug;
            var updated = Map(input);
            if (!await ValidateUniqueSlug(updated.Slug, article.Id)) return View("~/Views/NewsAdmin/Edit.cshtml", input);
            updated.NormalizedSourceUrl = NormalizeSourceUrl(input.SourceUrl);
            updated.NormalizedSourceUrlHash = HashSourceUrl(updated.NormalizedSourceUrl);
            if (!await ValidateUniqueSourceUrl(updated.NormalizedSourceUrlHash, article.Id)) return View("~/Views/NewsAdmin/Edit.cshtml", input);
            await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            CopyEditable(updated, article);
            article.UpdatedAt = DateTime.UtcNow;
            article.Editor = User.Identity.Name;
            article.NormalizedSourceUrl = updated.NormalizedSourceUrl;
            article.NormalizedSourceUrlHash = updated.NormalizedSourceUrlHash;
            if (!string.Equals(oldSlug, article.Slug, StringComparison.Ordinal))
            {
                var existingRedirect = await db.NewsSlugRedirects.FirstOrDefaultAsync(item => item.OldSlug == oldSlug, HttpContext.RequestAborted);
                if (existingRedirect == null)
                    db.NewsSlugRedirects.Add(new NewsSlugRedirect { OldSlug = oldSlug, ArticleId = article.Id, CreatedAt = DateTime.UtcNow });
                else if (existingRedirect.ArticleId != article.Id)
                {
                    ModelState.AddModelError(nameof(input.Slug), "Slug cũ đang được dùng làm đường dẫn chuyển hướng.");
                    return View("~/Views/NewsAdmin/Edit.cshtml", input);
                }
            }
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await AddRevision(article, "edited");
            await transaction.CommitAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "NewsAdmin"), HttpGet("xem-truoc/{id:long}")]
        public async Task<IActionResult> Preview(long id)
        {
            var article = await db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            if (article == null) return NotFound();
            NewsController.SetArticleMetadata(this, article, true, site);
            return View("~/Views/News/Detail.cshtml", new NewsArticleDetailViewModel { Article = article, IsPreview = true });
        }

        [Authorize(Roles = "NewsAdmin"), HttpPost("xem-truoc"), ValidateAntiForgeryToken]
        public IActionResult PreviewDraft(NewsArticleEditorViewModel input)
        {
            if (!ValidateArticle(input)) return View("~/Views/NewsAdmin/Edit.cshtml", input);
            var article = Map(input);
            article.UpdatedAt = DateTime.UtcNow;
            NewsController.SetArticleMetadata(this, article, true, site);
            return View("~/Views/News/Detail.cshtml", new NewsArticleDetailViewModel { Article = article, IsPreview = true });
        }

        [Authorize(Roles = "NewsAdmin"), HttpGet("lich-su/{id:long}")]
        public async Task<IActionResult> History(long id)
        {
            var article = await db.NewsArticles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            if (article == null) return NotFound();
            var model = new NewsArticleRevisionListViewModel
            {
                Article = article,
                Revisions = await db.NewsArticleRevisions.AsNoTracking().Where(item => item.ArticleId == id)
                    .OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(HttpContext.RequestAborted)
            };
            return View("~/Views/NewsAdmin/History.cshtml", model);
        }

        [Authorize(Roles = "NewsAdmin"), HttpPost("{id:long}/cho-duyet"), ValidateAntiForgeryToken]
        public Task<IActionResult> SubmitForReview(long id) =>
            ChangeStatus(id, new[] { NewsArticleStatuses.Draft, NewsArticleStatuses.Withdrawn }, NewsArticleStatuses.PendingReview);

        [Authorize(Roles = "NewsAdmin"), HttpPost("{id:long}/duyet"), ValidateAntiForgeryToken]
        public Task<IActionResult> Approve(long id) => Publish(id);

        [Authorize(Roles = "NewsAdmin"), HttpPost("{id:long}/go-bai"), ValidateAntiForgeryToken]
        public Task<IActionResult> Withdraw(long id) =>
            ChangeStatus(id, new[] { NewsArticleStatuses.Published }, NewsArticleStatuses.Withdrawn);

        [Authorize(Roles = "NewsAdmin"), HttpPost("lay-tin-ngay"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectNow()
        {
            var run = await collection.RunAsync(User.Identity.Name, HttpContext.RequestAborted);
            TempData["CollectionStatus"] = $"{run.Status}: {run.Message}";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "NewsAdmin"), HttpPost("nguon/{id:long}/da-kiem-tra"), ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkSourceReviewed(long id)
        {
            await sourceTracker.MarkReviewedAsync(id, User.Identity.Name, HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> Publish(long id)
        {
            var article = await db.NewsArticles.FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            if (article == null) return NotFound();
            if (article.Status != NewsArticleStatuses.PendingReview) return Conflict("Chỉ bài chờ duyệt mới được xuất bản.");
            article.Status = NewsArticleStatuses.Published;
            article.PublishedAt ??= DateTime.UtcNow;
            article.ApprovedAt = DateTime.UtcNow;
            article.ApprovedBy = User.Identity.Name;
            article.UpdatedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await AddRevision(article, "approved-and-published");
            await transaction.CommitAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ChangeStatus(long id, string[] allowedStatuses, string status)
        {
            var article = await db.NewsArticles.FirstOrDefaultAsync(item => item.Id == id, HttpContext.RequestAborted);
            if (article == null) return NotFound();
            if (!allowedStatuses.Contains(article.Status, StringComparer.Ordinal))
                return Conflict("Trạng thái hiện tại không cho phép thao tác này.");
            article.Status = status;
            article.Editor = User.Identity.Name;
            article.UpdatedAt = DateTime.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await AddRevision(article, status == NewsArticleStatuses.Withdrawn ? "withdrawn" : "submitted-for-review");
            await transaction.CommitAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        private bool ValidateArticle(NewsArticleEditorViewModel input)
        {
            input.Title = (input.Title ?? string.Empty).Trim();
            input.Summary = (input.Summary ?? string.Empty).Trim();
            input.Slug = LotteryUrls.Slug(string.IsNullOrWhiteSpace(input.Slug) ? input.Title : input.Slug);
            input.Author = (input.Author ?? string.Empty).Trim();
            input.SourceName = (input.SourceName ?? string.Empty).Trim();
            input.SourceUrl = (input.SourceUrl ?? string.Empty).Trim();
            input.Content ??= string.Empty;
            if (input.Title.Length is < 3 or > 240) ModelState.AddModelError(nameof(input.Title), "Tiêu đề cần từ 3 đến 240 ký tự.");
            if (input.Slug.Length is < 2 or > 180 || !Regex.IsMatch(input.Slug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z"))
                ModelState.AddModelError(nameof(input.Slug), "Slug chỉ gồm chữ thường, số và dấu gạch ngang.");
            if (input.Summary.Length > 500) ModelState.AddModelError(nameof(input.Summary), "Tóm tắt tối đa 500 ký tự.");
            if (input.Content.Length > 200000) ModelState.AddModelError(nameof(input.Content), "Nội dung tối đa 200.000 ký tự.");
            if (!Categories.Contains(input.Category ?? string.Empty)) ModelState.AddModelError(nameof(input.Category), "Danh mục không hợp lệ.");
            if (input.Author.Length > 160) ModelState.AddModelError(nameof(input.Author), "Tên tác giả tối đa 160 ký tự.");
            if ((input.FeaturedImageUrl?.Length ?? 0) > 500) ModelState.AddModelError(nameof(input.FeaturedImageUrl), "Đường dẫn ảnh tối đa 500 ký tự.");
            if ((input.FeaturedImageAlt?.Length ?? 0) > 300) ModelState.AddModelError(nameof(input.FeaturedImageAlt), "Mô tả ảnh tối đa 300 ký tự.");
            if ((input.SourceName?.Length ?? 0) > 240) ModelState.AddModelError(nameof(input.SourceName), "Tên nguồn tối đa 240 ký tự.");
            if ((input.SourceUrl?.Length ?? 0) > 1000) ModelState.AddModelError(nameof(input.SourceUrl), "URL nguồn tối đa 1.000 ký tự.");
            if ((input.SeoTitle?.Length ?? 0) > 240) ModelState.AddModelError(nameof(input.SeoTitle), "SEO title tối đa 240 ký tự.");
            if ((input.MetaDescription?.Length ?? 0) > 500) ModelState.AddModelError(nameof(input.MetaDescription), "Meta description tối đa 500 ký tự.");
            if ((input.PrizeRank?.Length ?? 0) > 80) ModelState.AddModelError(nameof(input.PrizeRank), "Hạng giải tối đa 80 ký tự.");
            if ((input.AwardLocation?.Length ?? 0) > 160) ModelState.AddModelError(nameof(input.AwardLocation), "Địa phương tối đa 160 ký tự.");
            if (string.IsNullOrWhiteSpace(input.SourceUrl) != string.IsNullOrWhiteSpace(input.SourceName))
                ModelState.AddModelError(nameof(input.SourceUrl), "Cần nhập đồng thời tên nguồn và URL nguồn.");
            if (!string.IsNullOrWhiteSpace(input.SourceUrl) && NormalizeSourceUrl(input.SourceUrl) == null)
                ModelState.AddModelError(nameof(input.SourceUrl), "URL nguồn phải là HTTPS công khai hợp lệ.");
            if (!ValidateImagePath(input.FeaturedImageUrl))
                ModelState.AddModelError(nameof(input.FeaturedImageUrl), "Ảnh chỉ được dùng từ wwwroot/images/news và phải là tệp JPG, PNG hoặc WebP có sẵn.");
            if (input.GameCode != null && !Games.Contains(input.GameCode))
                ModelState.AddModelError(nameof(input.GameCode), "Trò chơi Vietlott không hợp lệ.");
            if (!string.IsNullOrWhiteSpace(input.DrawNo) && !LotteryUrls.ValidDrawNo(input.DrawNo))
                ModelState.AddModelError(nameof(input.DrawNo), "Mã kỳ chỉ được gồm chữ số.");
            if (input.AnnouncedPrizeValue < 0 || input.RecipientAmount < 0 || input.WinningTicketCount < 0 || input.WinnerCount < 0)
                ModelState.AddModelError(string.Empty, "Giá trị và số lượng giải không được âm.");
            const decimal maxPrizeValue = 999999999999999999.99m;
            if (input.AnnouncedPrizeValue > maxPrizeValue || input.RecipientAmount > maxPrizeValue)
                ModelState.AddModelError(string.Empty, "Giá trị giải vượt giới hạn lưu trữ.");
            if (input.TaxTreatment != null && !TaxTreatments.Contains(input.TaxTreatment))
                ModelState.AddModelError(nameof(input.TaxTreatment), "Cách thể hiện thuế không hợp lệ.");
            var today = LotteryUrls.Today;
            if (input.SourcePublishedAt > today || input.CollectedAt > today || input.DrawDate > today || input.AwardedAt > today)
                ModelState.AddModelError(string.Empty, "Ngày nguồn, thu thập, quay và trao thưởng không được ở tương lai.");
            return ModelState.IsValid;
        }

        private bool ValidateImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (!path.StartsWith("/images/news/", StringComparison.Ordinal)
                || path.Contains('\\') || path.Contains('?') || path.Contains('#')
                || path.Split('/').Any(part => part == "." || part == "..")) return false;
            var extension = Path.GetExtension(path);
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
            var root = Path.GetFullPath(environment.WebRootPath);
            var fullPath = Path.GetFullPath(Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath);
        }

        private async Task<bool> ValidateUniqueSlug(string slug, long? currentId)
        {
            var exists = await db.NewsArticles.AnyAsync(article => article.Slug == slug && (!currentId.HasValue || article.Id != currentId.Value), HttpContext.RequestAborted);
            if (exists) ModelState.AddModelError(nameof(NewsArticleEditorViewModel.Slug), "Slug này đã được sử dụng.");
            return !exists;
        }

        private async Task<bool> ValidateUniqueSourceUrl(string hash, long? currentId)
        {
            if (hash == null) return true;
            var exists = await db.NewsArticles.AnyAsync(article => article.NormalizedSourceUrlHash == hash
                && (!currentId.HasValue || article.Id != currentId.Value), HttpContext.RequestAborted);
            if (exists) ModelState.AddModelError(nameof(NewsArticleEditorViewModel.SourceUrl), "URL nguồn này đã gắn với bài viết khác.");
            return !exists;
        }

        private async Task AddRevision(NewsArticle article, string changeType)
        {
            db.NewsArticleRevisions.Add(new NewsArticleRevision
            {
                ArticleId = article.Id,
                SnapshotJson = JsonSerializer.Serialize(article),
                ChangedBy = User.Identity.Name ?? "unknown",
                ChangeType = changeType,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(HttpContext.RequestAborted);
        }

        private static NewsArticle Map(NewsArticleEditorViewModel input) => new NewsArticle
        {
            Title = input.Title,
            Slug = input.Slug,
            Summary = input.Summary,
            Content = input.Content,
            Category = input.Category,
            FeaturedImageUrl = input.FeaturedImageUrl,
            FeaturedImageAlt = input.FeaturedImageAlt,
            Author = input.Author,
            SourceName = input.SourceName,
            SourceUrl = input.SourceUrl,
            SourcePublishedAt = input.SourcePublishedAt,
            CollectedAt = input.CollectedAt,
            SeoTitle = input.SeoTitle,
            MetaDescription = input.MetaDescription,
            GameCode = input.GameCode,
            DrawNo = input.DrawNo,
            DrawDate = input.DrawDate,
            PrizeRank = input.PrizeRank,
            AnnouncedPrizeValue = input.AnnouncedPrizeValue,
            WinningTicketCount = input.WinningTicketCount,
            WinnerCount = input.WinnerCount,
            RecipientAmount = input.RecipientAmount,
            TaxTreatment = input.TaxTreatment,
            AwardedAt = input.AwardedAt,
            AwardLocation = input.AwardLocation
        };

        private static NewsArticleEditorViewModel Map(NewsArticle article) => new NewsArticleEditorViewModel
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            Content = article.Content,
            Category = article.Category,
            FeaturedImageUrl = article.FeaturedImageUrl,
            FeaturedImageAlt = article.FeaturedImageAlt,
            Author = article.Author,
            SourceName = article.SourceName,
            SourceUrl = article.SourceUrl,
            SourcePublishedAt = article.SourcePublishedAt,
            CollectedAt = article.CollectedAt,
            SeoTitle = article.SeoTitle,
            MetaDescription = article.MetaDescription,
            GameCode = article.GameCode,
            DrawNo = article.DrawNo,
            DrawDate = article.DrawDate,
            PrizeRank = article.PrizeRank,
            AnnouncedPrizeValue = article.AnnouncedPrizeValue,
            WinningTicketCount = article.WinningTicketCount,
            WinnerCount = article.WinnerCount,
            RecipientAmount = article.RecipientAmount,
            TaxTreatment = article.TaxTreatment,
            AwardedAt = article.AwardedAt,
            AwardLocation = article.AwardLocation
        };

        private static void CopyEditable(NewsArticle source, NewsArticle target)
        {
            target.Title = source.Title;
            target.Slug = source.Slug;
            target.Summary = source.Summary;
            target.Content = source.Content;
            target.Category = source.Category;
            target.FeaturedImageUrl = source.FeaturedImageUrl;
            target.FeaturedImageAlt = source.FeaturedImageAlt;
            target.Author = source.Author;
            target.SourceName = source.SourceName;
            target.SourceUrl = source.SourceUrl;
            target.SourcePublishedAt = source.SourcePublishedAt;
            target.CollectedAt = source.CollectedAt;
            target.SeoTitle = source.SeoTitle;
            target.MetaDescription = source.MetaDescription;
            target.GameCode = source.GameCode;
            target.DrawNo = source.DrawNo;
            target.DrawDate = source.DrawDate;
            target.PrizeRank = source.PrizeRank;
            target.AnnouncedPrizeValue = source.AnnouncedPrizeValue;
            target.WinningTicketCount = source.WinningTicketCount;
            target.WinnerCount = source.WinnerCount;
            target.RecipientAmount = source.RecipientAmount;
            target.TaxTreatment = source.TaxTreatment;
            target.AwardedAt = source.AwardedAt;
            target.AwardLocation = source.AwardLocation;
        }

        private static string NormalizeSourceUrl(string value)
        {
            return NewsSourcePolicy.NormalizeEditorialUrl(value);
        }

        private static string HashSourceUrl(string normalizedUrl) => normalizedUrl == null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl)));

        private string GetRateLimitKey()
        {
            var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)));
            return "news-admin-login:" + hash;
        }
    }
}
