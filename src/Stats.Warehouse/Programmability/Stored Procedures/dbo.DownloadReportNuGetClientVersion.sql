CREATE PROCEDURE [dbo].[DownloadReportNuGetClientVersion]
AS
BEGIN
	SET NOCOUNT ON;

	-- Find all clients that have had download facts added in the last 42 days
	SELECT	Client.[Major],
			Client.[Minor],
			SUM(ISNULL(Facts.DownloadCount, 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Client] AS Client (NOLOCK)
	ON			Client.[Id] = Facts.[Dimension_Client_Id]

	WHERE ISNULL(Facts.[Timestamp], CONVERT(DATETIME, '1900-01-01')) > CONVERT(DATE, DATEADD(day, -42, GETDATE()))
	  AND Client.[ClientCategory] = 'NuGet'

	GROUP BY Client.[Major], Client.[Minor]
	ORDER BY	[Major], [Minor]

END