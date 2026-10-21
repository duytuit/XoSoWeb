

using Microsoft.EntityFrameworkCore;
using XoSoWeb.Models;
using XoSoWeb.Lotterys.Models;

namespace XoSoWeb.Shares.BaseRepository
{
    public class XoSoDBContext : DbContext
    {
        public DbSet<Lottery> Lotteries { get; set; }
        public DbSet<NewsArticle> NewsArticles { get; set; }
        public DbSet<NewsArticleRevision> NewsArticleRevisions { get; set; }
        public DbSet<NewsSourceRecord> NewsSourceRecords { get; set; }
        public DbSet<NewsCollectionRun> NewsCollectionRuns { get; set; }
        public DbSet<NewsSlugRedirect> NewsSlugRedirects { get; set; }
        public XoSoDBContext(DbContextOptions<XoSoDBContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuider)
        {
            modelBuider.Entity<Lottery>().HasQueryFilter(e => e.DeletedAt == null);
            modelBuider.Entity<NewsArticle>().HasIndex(article => article.Slug).IsUnique();
            modelBuider.Entity<NewsArticle>().HasIndex(article => new { article.Status, article.PublishedAt });
            modelBuider.Entity<NewsArticle>().HasIndex(article => new { article.Category, article.Status, article.PublishedAt });
            modelBuider.Entity<NewsArticle>().HasIndex(article => article.NormalizedSourceUrlHash).IsUnique()
                .HasFilter("[NormalizedSourceUrlHash] IS NOT NULL");
            modelBuider.Entity<NewsArticleRevision>().HasIndex(revision => new { revision.ArticleId, revision.CreatedAt });
            modelBuider.Entity<NewsSourceRecord>().HasIndex(record => new { record.SourceKey, record.NormalizedUrlHash }).IsUnique();
            modelBuider.Entity<NewsArticleRevision>().HasOne<NewsArticle>().WithMany()
                .HasForeignKey(revision => revision.ArticleId).OnDelete(DeleteBehavior.Cascade);
            modelBuider.Entity<NewsSourceRecord>().HasOne<NewsArticle>().WithMany()
                .HasForeignKey(record => record.ArticleId).OnDelete(DeleteBehavior.SetNull);
            modelBuider.Entity<NewsSlugRedirect>().HasOne<NewsArticle>().WithMany()
                .HasForeignKey(redirect => redirect.ArticleId).OnDelete(DeleteBehavior.Cascade);
            modelBuider.Entity<NewsArticle>().Property(article => article.NormalizedSourceUrlHash).HasColumnType("char(64)");
            modelBuider.Entity<NewsArticle>().Property(article => article.SourceContentHash).HasColumnType("char(64)");
            modelBuider.Entity<NewsSourceRecord>().Property(record => record.NormalizedUrlHash).HasColumnType("char(64)");
            modelBuider.Entity<NewsSourceRecord>().Property(record => record.ContentHash).HasColumnType("char(64)");
            modelBuider.Entity<NewsCollectionRun>().HasIndex(run => run.StartedAt);
            modelBuider.Entity<NewsSlugRedirect>().HasIndex(redirect => redirect.OldSlug).IsUnique();
        }           
    }
}
