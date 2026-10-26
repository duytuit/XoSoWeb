-- Default: preview only. Set @Apply = 1 to archive and remove byte-identical duplicates.
-- Scope: TP HCM on 24/10/2026 only; conflicting payloads are preserved.
-- Deploy the xsmbsocket repository fix before relying on future writes being idempotent.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Apply bit = 0;
BEGIN TRY
    BEGIN TRANSACTION;
    SELECT r.Id INTO #DuplicateIds
    FROM dbo.XoSoResults r WITH (TABLOCKX, HOLDLOCK)
    WHERE r.DrawDate >= '20261024' AND r.DrawDate < '20261025'
      AND r.ProvinceCode = 'HCM' AND r.ProvinceName = N'TP HCM'
      AND EXISTS (
          SELECT 1 FROM dbo.XoSoResults keepRow
          WHERE keepRow.Id > r.Id
            AND keepRow.DrawDate = r.DrawDate
            AND keepRow.Region = r.Region
            AND keepRow.ProvinceCode = r.ProvinceCode
            AND keepRow.ProvinceName = r.ProvinceName
            AND CONVERT(varbinary(max), keepRow.Prizes) = CONVERT(varbinary(max), r.Prizes)
      );
    SELECT COUNT(*) AS DuplicateRowsToArchive FROM #DuplicateIds;
    IF @Apply = 1 AND EXISTS (SELECT 1 FROM #DuplicateIds)
    BEGIN
        IF OBJECT_ID('dbo.XoSoResultsDuplicateBackup_20261024_HCM', 'U') IS NOT NULL
            RAISERROR('Backup table already exists. Review it before another cleanup.', 16, 1);
        -- Retain every original column, including timestamps and source metadata if present.
        SELECT r.*, SYSUTCDATETIME() AS DuplicateArchivedAtUtc
        INTO dbo.XoSoResultsDuplicateBackup_20261024_HCM
        FROM dbo.XoSoResults r JOIN #DuplicateIds d ON d.Id = r.Id;
        DECLARE @Expected int = (SELECT COUNT(*) FROM #DuplicateIds);
        DELETE r FROM dbo.XoSoResults r JOIN #DuplicateIds d ON d.Id = r.Id;
        IF @@ROWCOUNT <> @Expected RAISERROR('Cleanup count mismatch.', 16, 1);
        SELECT @Expected AS ArchivedAndRemovedRows;
    END;
    DROP TABLE #DuplicateIds;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT ProvinceCode, ProvinceName, COUNT(*) AS RemainingRows
FROM dbo.XoSoResults
WHERE DrawDate >= '20261024' AND DrawDate < '20261025' AND Region IN (N'MN', N'Miền Nam')
GROUP BY ProvinceCode, ProvinceName;
