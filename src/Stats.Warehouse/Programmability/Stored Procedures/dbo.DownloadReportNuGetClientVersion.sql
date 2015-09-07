CREATE PROCEDURE [dbo].[DownloadReportNuGetClientVersion]
AS
BEGIN
	SET NOCOUNT ON;

	-- Find all clients that have had download facts added in the last 42 days, today inclusive
	SELECT	Client.[Major],
			Client.[Minor],
			SUM(ISNULL(Facts.DownloadCount, 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			D.[Id] = Facts.[Dimension_Date_Id]

	INNER JOIN	[dbo].[Dimension_Client] AS Client (NOLOCK)
	ON			Client.[Id] = Facts.[Dimension_Client_Id]

	WHERE	D.[Date] IS NOT NULL
		AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
		AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, GETDATE()))) <= CONVERT(DATE, GETDATE())
		AND Client.[ClientCategory] = 'NuGet'
		AND Client.[Major] <> 99

	GROUP BY Client.[Major], Client.[Minor]
	ORDER BY	[Major], [Minor]
END