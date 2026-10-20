SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Keep every original result, including conflicting source payloads. Never delete history.
IF OBJECT_ID('dbo.StationNormalizationAudit', 'U') IS NULL
    SELECT TOP (0) Id + CAST(0 AS bigint) AS ResultId, Region, ProvinceCode, ProvinceName, DrawDate, Prizes, CreatedAt, UpdatedAt
    INTO dbo.StationNormalizationAudit FROM dbo.XoSoResults;
IF COL_LENGTH('dbo.XoSoResults', 'SourceName') IS NULL
    ALTER TABLE dbo.XoSoResults ADD SourceName nvarchar(100) NOT NULL CONSTRAINT DF_XoSoSource DEFAULT N'legacy-unknown';
IF COL_LENGTH('dbo.XoSoResults', 'SourcePriority') IS NULL
    ALTER TABLE dbo.XoSoResults ADD SourcePriority int NOT NULL CONSTRAINT DF_XoSoSourcePriority DEFAULT 0;
COMMIT;
GO
IF OBJECT_ID('dbo.NormalizeLotteryStation', 'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.NormalizeLotteryStation ON dbo.XoSoResults AFTER INSERT AS BEGIN SET NOCOUNT ON; END');
GO
ALTER TRIGGER dbo.NormalizeLotteryStation ON dbo.XoSoResults
AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;
    INSERT dbo.StationNormalizationAudit
        (ResultId, Region, ProvinceCode, ProvinceName, DrawDate, Prizes, CreatedAt, UpdatedAt)
    SELECT i.Id, i.Region, i.ProvinceCode, i.ProvinceName, i.DrawDate, i.Prizes, i.CreatedAt, i.UpdatedAt
    FROM inserted i
    WHERE NOT EXISTS (SELECT 1 FROM dbo.StationNormalizationAudit a WHERE a.ResultId = i.Id);
    UPDATE r SET ProvinceCode = s.Code, ProvinceName = s.Name, Region = s.Region
    FROM dbo.XoSoResults r JOIN inserted i ON i.Id = r.Id
    CROSS APPLY (SELECT TOP (1) v.Code, v.Name, v.Region FROM (VALUES
(N'TPHCM', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số TPHCM', N'HCM', N'TP HCM', N'MN'),
(N'TP.HCM', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số TP.HCM', N'HCM', N'TP HCM', N'MN'),
(N'TP. HCM', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số TP. HCM', N'HCM', N'TP HCM', N'MN'),
(N'Ho Chi Minh', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số Ho Chi Minh', N'HCM', N'TP HCM', N'MN'),
(N'Thành phố Hồ Chí Minh', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số Thành phố Hồ Chí Minh', N'HCM', N'TP HCM', N'MN'),
(N'HCM', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số HCM', N'HCM', N'TP HCM', N'MN'),
(N'TP HCM', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số TP HCM', N'HCM', N'TP HCM', N'MN'),
(N'tp-hcm', N'HCM', N'TP HCM', N'MN'),
(N'Xổ số tp-hcm', N'HCM', N'TP HCM', N'MN'),
(N'Dac Lac', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số Dac Lac', N'DL', N'Đắk Lắk', N'MT'),
(N'Đắc Lắc', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số Đắc Lắc', N'DL', N'Đắk Lắk', N'MT'),
(N'Daklak', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số Daklak', N'DL', N'Đắk Lắk', N'MT'),
(N'DACLAC', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số DACLAC', N'DL', N'Đắk Lắk', N'MT'),
(N'DLK', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số DLK', N'DL', N'Đắk Lắk', N'MT'),
(N'DL', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số DL', N'DL', N'Đắk Lắk', N'MT'),
(N'Đắk Lắk', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số Đắk Lắk', N'DL', N'Đắk Lắk', N'MT'),
(N'dak-lak', N'DL', N'Đắk Lắk', N'MT'),
(N'Xổ số dak-lak', N'DL', N'Đắk Lắk', N'MT'),
(N'Dac Nong', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số Dac Nong', N'DNO', N'Đắk Nông', N'MT'),
(N'Đắc Nông', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số Đắc Nông', N'DNO', N'Đắk Nông', N'MT'),
(N'Daknong', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số Daknong', N'DNO', N'Đắk Nông', N'MT'),
(N'DACNONG', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số DACNONG', N'DNO', N'Đắk Nông', N'MT'),
(N'DNONG', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số DNONG', N'DNO', N'Đắk Nông', N'MT'),
(N'DNO', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số DNO', N'DNO', N'Đắk Nông', N'MT'),
(N'Đắk Nông', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số Đắk Nông', N'DNO', N'Đắk Nông', N'MT'),
(N'dak-nong', N'DNO', N'Đắk Nông', N'MT'),
(N'Xổ số dak-nong', N'DNO', N'Đắk Nông', N'MT')
    ) v(Alias, Code, Name, Region)
    WHERE i.ProvinceCode = v.Alias OR i.ProvinceName = v.Alias
    ORDER BY CASE WHEN i.ProvinceCode = v.Alias THEN 0 ELSE 1 END, v.Code
    ) s;
END;
GO
-- Run once to normalize existing history. Trigger records original identities first.
UPDATE dbo.XoSoResults SET ProvinceCode = ProvinceCode;
GO
-- Review competing records; payloads remain available for source verification.
SELECT ProvinceCode, CAST(DrawDate AS date) AS DrawDate, COUNT(*) AS SourceRows
FROM dbo.XoSoResults
GROUP BY ProvinceCode, CAST(DrawDate AS date)
HAVING COUNT(*) > 1;
