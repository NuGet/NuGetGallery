CREATE PROCEDURE [dbo].[SelectTotalDownloadCounts]
AS
BEGIN
	SET NOCOUNT ON;

	-- select total # of downloads + correction from gallery database/old warehouse
	SELECT	SUM(ISNULL(F.[DownloadCount], 0)) - 51000000 AS [Downloads]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F
END