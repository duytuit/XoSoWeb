IF COL_LENGTH(N'dbo.NewsArticles', N'ThumbnailImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.NewsArticles ADD ThumbnailImageUrl nvarchar(500) NULL;
END;
