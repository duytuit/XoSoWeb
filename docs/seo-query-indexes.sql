-- Review against the actual production schema and existing indexes before applying.
-- No database changes have been executed by this task.
-- Measure actual plans + SET STATISTICS IO, TIME ON before and after.
-- Region aliases currently include MB/MT/MN and stored Vietnamese names.

IF OBJECT_ID(N'dbo.XoSoResults', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.XoSoResults') AND name = N'IX_XoSoResults_Region_DrawDate')
CREATE INDEX IX_XoSoResults_Region_DrawDate
ON dbo.XoSoResults(Region, DrawDate DESC)
INCLUDE(ProvinceCode, ProvinceName, UpdatedAt);

IF OBJECT_ID(N'dbo.XoSoResults', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.XoSoResults') AND name = N'IX_XoSoResults_Province_DrawDate')
CREATE INDEX IX_XoSoResults_Province_DrawDate
ON dbo.XoSoResults(ProvinceCode, DrawDate DESC)
INCLUDE(Region, ProvinceName, UpdatedAt);

IF OBJECT_ID(N'dbo.VietlottResults', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VietlottResults') AND name = N'IX_VietlottResults_Game_DrawDate_Id')
CREATE INDEX IX_VietlottResults_Game_DrawDate_Id
ON dbo.VietlottResults(GameCode, DrawDate DESC, Id DESC)
INCLUDE(DrawNo);

IF OBJECT_ID(N'dbo.VietlottResults', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VietlottResults') AND name = N'IX_VietlottResults_Game_DrawNo')
CREATE INDEX IX_VietlottResults_Game_DrawNo
ON dbo.VietlottResults(GameCode, DrawNo) INCLUDE(DrawDate);

IF OBJECT_ID(N'dbo.VietlottPrizes', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.VietlottPrizes') AND name = N'IX_VietlottPrizes_ResultId_Id')
CREATE INDEX IX_VietlottPrizes_ResultId_Id
ON dbo.VietlottPrizes(ResultId, Id);

-- Statistics use DrawDate ranges across all regions. Consider (DrawDate, Region)
-- only after checking selectivity and write costs in actual plans.
-- Large JSON/Numbers columns are deliberately excluded to avoid bloating write indexes.
