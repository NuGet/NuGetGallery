CREATE PROCEDURE [dbo].[SelectTotalDownloadCounts]
AS
BEGIN
	SET NOCOUNT ON;

	-- select total # of downloads + correction for packages that can not be mapped
	SELECT	SUM(ISNULL(F.[DownloadCount], 0)) - 21000000 AS [Downloads]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F
END