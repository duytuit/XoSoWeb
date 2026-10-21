using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using XoSoWeb.Models;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Services
{
    public sealed class NewsSourceObservation
    {
        public string SourceKey { get; set; }
        public string Url { get; set; }
        public string SourceId { get; set; }
        public string ContentHash { get; set; }
        public string GameCode { get; set; }
        public string DrawNo { get; set; }
        public string ETag { get; set; }
        public DateTime? SourceLastModified { get; set; }
    }

    public sealed class NewsSourceObservationResult
    {
        public string Outcome { get; set; }
        public NewsSourceRecord Record { get; set; }
    }

    public sealed class NewsSourceChangeTracker
    {
        private readonly XoSoDBContext db;
        public NewsSourceChangeTracker(XoSoDBContext db) => this.db = db;

        public async Task<NewsSourceObservationResult> RecordAsync(NewsSourceObservation observation, CancellationToken cancellationToken)
        {
            if (observation == null || observation.SourceKey != "vietlott"
                || !NewsSourcePolicy.TryNormalizeVietlottUrl(observation.Url, out var normalizedUrl)
                || normalizedUrl.Length > 1000
                || !System.Text.RegularExpressions.Regex.IsMatch(observation.ContentHash ?? string.Empty, @"\A[0-9A-Fa-f]{64}\z")
                || (observation.SourceId?.Length ?? 0) > 500
                || (observation.ETag?.Length ?? 0) > 512)
                throw new ArgumentException("Source observation is outside the verified Vietlott URL/hash policy.", nameof(observation));

            if (observation.GameCode != null && !ArchiveCatalog.SupportedGames.Contains(observation.GameCode, StringComparer.Ordinal))
                throw new ArgumentException("Source observation contains an unsupported game code.", nameof(observation));
            if (observation.DrawNo != null && !LotteryUrls.ValidDrawNo(observation.DrawNo))
                throw new ArgumentException("Source observation contains an invalid draw identifier.", nameof(observation));

            var normalizedHash = NewsSourcePolicy.UrlHash(normalizedUrl);
            var record = await db.NewsSourceRecords.FirstOrDefaultAsync(item =>
                item.SourceKey == observation.SourceKey && item.NormalizedUrlHash == normalizedHash, cancellationToken);
            var now = DateTime.UtcNow;
            if (record == null)
            {
                record = new NewsSourceRecord
                {
                    SourceKey = observation.SourceKey,
                    NormalizedUrl = normalizedUrl,
                    NormalizedUrlHash = normalizedHash,
                    SourceId = observation.SourceId,
                    ContentHash = observation.ContentHash.ToUpperInvariant(),
                    GameCode = observation.GameCode,
                    DrawNo = observation.DrawNo,
                    ETag = observation.ETag,
                    SourceLastModified = observation.SourceLastModified,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    ReviewStatus = "NeedsEditorialReview"
                };
                db.NewsSourceRecords.Add(record);
                await db.SaveChangesAsync(cancellationToken);
                return new NewsSourceObservationResult { Outcome = "New", Record = record };
            }

            record.LastSeenAt = now;
            record.SourceId = observation.SourceId ?? record.SourceId;
            record.GameCode = observation.GameCode ?? record.GameCode;
            record.DrawNo = observation.DrawNo ?? record.DrawNo;
            record.ETag = observation.ETag ?? record.ETag;
            record.SourceLastModified = observation.SourceLastModified ?? record.SourceLastModified;
            if (!string.Equals(record.ContentHash, observation.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                record.ContentHash = observation.ContentHash.ToUpperInvariant();
                record.ChangeDetectedAt = now;
                record.ReviewStatus = "NeedsReview";
                record.ReviewedBy = null;
                record.ReviewedAt = null;
                await db.SaveChangesAsync(cancellationToken);
                return new NewsSourceObservationResult { Outcome = "ChangedNeedsReview", Record = record };
            }

            await db.SaveChangesAsync(cancellationToken);
            return new NewsSourceObservationResult { Outcome = "Duplicate", Record = record };
        }

        public Task<System.Collections.Generic.List<NewsSourceRecord>> FindPossibleSameEventAsync(
            string gameCode, string drawNo, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(gameCode) || string.IsNullOrWhiteSpace(drawNo)
                || !ArchiveCatalog.SupportedGames.Contains(gameCode, StringComparer.Ordinal)
                || !LotteryUrls.ValidDrawNo(drawNo))
                throw new ArgumentException("A verified game code and draw number are required.");
            return db.NewsSourceRecords.AsNoTracking()
                .Where(record => record.GameCode == gameCode && record.DrawNo == drawNo)
                .OrderByDescending(record => record.LastSeenAt).Take(20).ToListAsync(cancellationToken);
        }

        public async Task MarkReviewedAsync(long id, string reviewer, CancellationToken cancellationToken)
        {
            var record = await db.NewsSourceRecords.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (record == null) throw new KeyNotFoundException("Source record not found.");
            record.ReviewStatus = "Reviewed";
            record.ReviewedBy = reviewer;
            record.ReviewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
