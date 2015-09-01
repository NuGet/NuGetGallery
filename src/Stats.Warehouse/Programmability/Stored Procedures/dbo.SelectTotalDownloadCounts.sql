CREATE PROCEDURE [dbo].[SelectTotalDownloadCounts]
AS
BEGIN
	SET NOCOUNT ON;

	-- select total # of downloads + correction from gallery database/old warehouse
	-- 208000000 is an approximate
	SELECT	SUM(ISNULL(F.[DownloadCount], 0)) + 208000000 AS [Downloads]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F
END