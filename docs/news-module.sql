CREATE TABLE dbo.NewsArticles
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsArticles PRIMARY KEY,
    Title nvarchar(240) NOT NULL,
    Slug nvarchar(180) NOT NULL,
    Summary nvarchar(500) NULL,
    Content nvarchar(max) NULL,
    Category nvarchar(40) NOT NULL,
    FeaturedImageUrl nvarchar(500) NULL,
    FeaturedImageAlt nvarchar(300) NULL,
    Author nvarchar(160) NULL,
    Editor nvarchar(160) NULL,
    SourceName nvarchar(240) NULL,
    SourceUrl nvarchar(1000) NULL,
    SourcePublishedAt datetime2 NULL,
    CollectedAt datetime2 NULL,
    PublishedAt datetime2 NULL,
    CreatedAt datetime2 NOT NULL,
    UpdatedAt datetime2 NOT NULL,
    Status nvarchar(30) NOT NULL,
    SeoTitle nvarchar(240) NULL,
    MetaDescription nvarchar(500) NULL,
    GameCode nvarchar(20) NULL,
    DrawNo nvarchar(40) NULL,
    DrawDate datetime2 NULL,
    PrizeRank nvarchar(80) NULL,
    AnnouncedPrizeValue decimal(20,2) NULL,
    WinningTicketCount int NULL,
    WinnerCount int NULL,
    RecipientAmount decimal(20,2) NULL,
    TaxTreatment nvarchar(20) NULL,
    AwardedAt datetime2 NULL,
    AwardLocation nvarchar(160) NULL,
    NormalizedSourceUrl nvarchar(1000) NULL,
    NormalizedSourceUrlHash char(64) NULL,
    SourceContentHash char(64) NULL,
    ApprovedBy nvarchar(160) NULL,
    ApprovedAt datetime2 NULL,
    CONSTRAINT CK_NewsArticles_Status CHECK (Status IN (N'Draft', N'PendingReview', N'Published', N'Withdrawn'))
);

CREATE UNIQUE INDEX UX_NewsArticles_Slug ON dbo.NewsArticles(Slug);
CREATE UNIQUE INDEX UX_NewsArticles_NormalizedSourceUrlHash
    ON dbo.NewsArticles(NormalizedSourceUrlHash) WHERE NormalizedSourceUrlHash IS NOT NULL;
CREATE INDEX IX_NewsArticles_Status_PublishedAt ON dbo.NewsArticles(Status, PublishedAt);
CREATE INDEX IX_NewsArticles_Category_Status_PublishedAt ON dbo.NewsArticles(Category, Status, PublishedAt);

CREATE TABLE dbo.NewsArticleRevisions
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsArticleRevisions PRIMARY KEY,
    ArticleId bigint NOT NULL,
    SnapshotJson nvarchar(max) NULL,
    ChangedBy nvarchar(160) NOT NULL,
    ChangeType nvarchar(40) NOT NULL,
    CreatedAt datetime2 NOT NULL,
    CONSTRAINT FK_NewsArticleRevisions_NewsArticles FOREIGN KEY (ArticleId)
        REFERENCES dbo.NewsArticles(Id) ON DELETE CASCADE
);
CREATE INDEX IX_NewsArticleRevisions_ArticleId_CreatedAt
    ON dbo.NewsArticleRevisions(ArticleId, CreatedAt);

CREATE TABLE dbo.NewsSourceRecords
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsSourceRecords PRIMARY KEY,
    SourceKey nvarchar(40) NOT NULL,
    NormalizedUrl nvarchar(1000) NOT NULL,
    NormalizedUrlHash char(64) NOT NULL,
    SourceId nvarchar(500) NULL,
    ContentHash char(64) NULL,
    ArticleId bigint NULL,
    FirstSeenAt datetime2 NOT NULL,
    LastSeenAt datetime2 NOT NULL,
    ChangeDetectedAt datetime2 NULL,
    ReviewStatus nvarchar(40) NULL,
    GameCode nvarchar(20) NULL,
    DrawNo nvarchar(40) NULL,
    ETag nvarchar(512) NULL,
    SourceLastModified datetime2 NULL,
    ReviewedBy nvarchar(160) NULL,
    ReviewedAt datetime2 NULL,
    CONSTRAINT FK_NewsSourceRecords_NewsArticles FOREIGN KEY (ArticleId)
        REFERENCES dbo.NewsArticles(Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX UX_NewsSourceRecords_SourceKey_NormalizedUrlHash
    ON dbo.NewsSourceRecords(SourceKey, NormalizedUrlHash);

CREATE TABLE dbo.NewsCollectionRuns
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsCollectionRuns PRIMARY KEY,
    StartedAt datetime2 NOT NULL,
    CompletedAt datetime2 NULL,
    Status nvarchar(40) NULL,
    FoundCount int NOT NULL,
    NewDraftCount int NOT NULL,
    DuplicateCount int NOT NULL,
    ChangedSourceCount int NOT NULL,
    ErrorCount int NOT NULL,
    Message nvarchar(1000) NULL,
    TriggeredBy nvarchar(100) NULL
);
CREATE INDEX IX_NewsCollectionRuns_StartedAt ON dbo.NewsCollectionRuns(StartedAt);

CREATE TABLE dbo.NewsSlugRedirects
(
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NewsSlugRedirects PRIMARY KEY,
    OldSlug nvarchar(180) NOT NULL,
    ArticleId bigint NOT NULL,
    CreatedAt datetime2 NOT NULL,
    CONSTRAINT FK_NewsSlugRedirects_NewsArticles FOREIGN KEY (ArticleId)
        REFERENCES dbo.NewsArticles(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_NewsSlugRedirects_OldSlug ON dbo.NewsSlugRedirects(OldSlug);
