using System;
using System.ComponentModel.DataAnnotations;

namespace XoSoWeb.Models
{
    public static class NewsArticleStatuses
    {
        public const string Draft = "Draft";
        public const string PendingReview = "PendingReview";
        public const string Published = "Published";
        public const string Withdrawn = "Withdrawn";
    }

    public static class NewsArticleCategories
    {
        public const string Vietlott = "vietlott";
        public const string Winners = "trung-thuong-vietlott";
        public const string Guides = "huong-dan";
        public const string General = "tin-tuc";
    }

    public sealed class NewsArticle
    {
        public long Id { get; set; }
        [Required, StringLength(240)]
        public string Title { get; set; }
        [Required, StringLength(180)]
        public string Slug { get; set; }
        [StringLength(500)]
        public string Summary { get; set; }
        public string Content { get; set; }
        [Required, StringLength(40)]
        public string Category { get; set; }
        [StringLength(500)]
        public string FeaturedImageUrl { get; set; }
        [StringLength(300)]
        public string FeaturedImageAlt { get; set; }
        [StringLength(160)]
        public string Author { get; set; }
        [StringLength(160)]
        public string Editor { get; set; }
        [StringLength(240)]
        public string SourceName { get; set; }
        [StringLength(1000)]
        public string SourceUrl { get; set; }
        public DateTime? SourcePublishedAt { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        [Required, StringLength(30)]
        public string Status { get; set; }
        [StringLength(240)]
        public string SeoTitle { get; set; }
        [StringLength(500)]
        public string MetaDescription { get; set; }
        [StringLength(20)]
        public string GameCode { get; set; }
        [StringLength(40)]
        public string DrawNo { get; set; }
        public DateTime? DrawDate { get; set; }
        [StringLength(80)]
        public string PrizeRank { get; set; }
        public decimal? AnnouncedPrizeValue { get; set; }
        public int? WinningTicketCount { get; set; }
        public int? WinnerCount { get; set; }
        public decimal? RecipientAmount { get; set; }
        [StringLength(20)]
        public string TaxTreatment { get; set; }
        public DateTime? AwardedAt { get; set; }
        [StringLength(160)]
        public string AwardLocation { get; set; }
        [StringLength(1000)]
        public string NormalizedSourceUrl { get; set; }
        [StringLength(64)]
        public string NormalizedSourceUrlHash { get; set; }
        [StringLength(64)]
        public string SourceContentHash { get; set; }
        [StringLength(160)]
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public sealed class NewsArticleRevision
    {
        public long Id { get; set; }
        public long ArticleId { get; set; }
        public string SnapshotJson { get; set; }
        [Required, StringLength(160)]
        public string ChangedBy { get; set; }
        [Required, StringLength(40)]
        public string ChangeType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class NewsSourceRecord
    {
        public long Id { get; set; }
        [Required, StringLength(40)]
        public string SourceKey { get; set; }
        [Required, StringLength(1000)]
        public string NormalizedUrl { get; set; }
        [Required, StringLength(64)]
        public string NormalizedUrlHash { get; set; }
        [StringLength(500)]
        public string SourceId { get; set; }
        [StringLength(64)]
        public string ContentHash { get; set; }
        public long? ArticleId { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime? ChangeDetectedAt { get; set; }
        [StringLength(40)]
        public string ReviewStatus { get; set; }
        [StringLength(20)]
        public string GameCode { get; set; }
        [StringLength(40)]
        public string DrawNo { get; set; }
        [StringLength(512)]
        public string ETag { get; set; }
        public DateTime? SourceLastModified { get; set; }
        [StringLength(160)]
        public string ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public sealed class NewsCollectionRun
    {
        public long Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        [StringLength(40)]
        public string Status { get; set; }
        public int FoundCount { get; set; }
        public int NewDraftCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ChangedSourceCount { get; set; }
        public int ErrorCount { get; set; }
        [StringLength(1000)]
        public string Message { get; set; }
        [StringLength(100)]
        public string TriggeredBy { get; set; }
    }

    public sealed class NewsSlugRedirect
    {
        public long Id { get; set; }
        [Required, StringLength(180)]
        public string OldSlug { get; set; }
        public long ArticleId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class NewsArticleListItem
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public string FeaturedImageUrl { get; set; }
        public string FeaturedImageAlt { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class NewsArticleListViewModel
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public int Page { get; set; }
        public int PageCount { get; set; }
        public System.Collections.Generic.List<NewsArticleListItem> Articles { get; set; } =
            new System.Collections.Generic.List<NewsArticleListItem>();
    }

    public sealed class NewsArticleEditorViewModel
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; }
        public string Category { get; set; }
        public string FeaturedImageUrl { get; set; }
        public string FeaturedImageAlt { get; set; }
        public string Author { get; set; }
        public string SourceName { get; set; }
        public string SourceUrl { get; set; }
        public DateTime? SourcePublishedAt { get; set; }
        public DateTime? CollectedAt { get; set; }
        public string SeoTitle { get; set; }
        public string MetaDescription { get; set; }
        public string GameCode { get; set; }
        public string DrawNo { get; set; }
        public DateTime? DrawDate { get; set; }
        public string PrizeRank { get; set; }
        public decimal? AnnouncedPrizeValue { get; set; }
        public int? WinningTicketCount { get; set; }
        public int? WinnerCount { get; set; }
        public decimal? RecipientAmount { get; set; }
        public string TaxTreatment { get; set; }
        public DateTime? AwardedAt { get; set; }
        public string AwardLocation { get; set; }
    }

    public sealed class NewsArticleDetailViewModel
    {
        public NewsArticle Article { get; set; }
        public bool IsPreview { get; set; }
    }

    public sealed class NewsAdminListViewModel
    {
        public System.Collections.Generic.List<NewsArticle> Articles { get; set; } =
            new System.Collections.Generic.List<NewsArticle>();
        public System.Collections.Generic.List<NewsCollectionRun> Runs { get; set; } =
            new System.Collections.Generic.List<NewsCollectionRun>();
        public System.Collections.Generic.List<NewsSourceRecord> SourceReviews { get; set; } =
            new System.Collections.Generic.List<NewsSourceRecord>();
        public string CollectionStatus { get; set; }
    }

    public sealed class NewsArticleRevisionListViewModel
    {
        public NewsArticle Article { get; set; }
        public System.Collections.Generic.List<NewsArticleRevision> Revisions { get; set; } =
            new System.Collections.Generic.List<NewsArticleRevision>();
    }
}
