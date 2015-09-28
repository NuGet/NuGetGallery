CREATE PROCEDURE [dbo].[DownloadReportRecentPopularityDetailByPackage]
	@ReportGenerationTime DATETIME,
	@PackageId NVARCHAR(128)
AS
BEGIN
	-- Find all packages that have had download facts added in the last 42 days, today included
	SET NOCOUNT ON;

	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')

	SELECT
			P.PackageVersion,
			C.ClientCategory,
			CASE
				WHEN	C.ClientCategory = 'Script' THEN 'Scripted Downloads'
				WHEN	C.ClientCategory = 'Browser' THEN 'Browsers'
				WHEN	C.ClientCategory = 'Mobile' THEN 'Browsers (Mobile)'
				WHEN	C.ClientName = 'NuGet' THEN 'NuGet.Core-based Downloads'
				WHEN	C.ClientName = 'NuGet Shim' THEN 'NuGet Client V3'
				ELSE	C.ClientName
			END AS ClientName,
			CASE
				WHEN	C.ClientCategory IN ('Script', 'Browser', 'Mobile') THEN '0'
				ELSE	C.Major
			END AS Major,
			CASE
				WHEN	C.ClientCategory IN ('Script', 'Browser', 'Mobile') THEN '0'
				ELSE	C.Minor
			END AS Minor,
			O.Operation,
			SUM(ISNULL(F.DownloadCount, 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS F (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
	ON			P.[Id] = F.[Dimension_Package_Id]

	INNER JOIN	Dimension_Date AS D (NOLOCK)
	ON			D.[Id] = F.[Dimension_Date_Id]

	INNER JOIN	Dimension_Operation AS O (NOLOCK)
	ON			O.[Id] = F.[Dimension_Operation_Id]

	INNER JOIN	Dimension_Client AS C (NOLOCK)
	ON			C.[Id] = F.[Dimension_Client_Id]

	WHERE		D.[Date] IS NOT NULL
			AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >= CONVERT(DATE, DATEADD(day, -42, @ReportGenerationTime))
			AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, @ReportGenerationTime))) <= CONVERT(DATE, @ReportGenerationTime)
			AND F.[Timestamp] <= @Cursor
			AND P.PackageId = @PackageId
			AND C.ClientCategory NOT IN ('Crawler', 'Unknown')
			AND NOT (C.ClientCategory = 'NuGet' AND CAST(ISNULL(C.[Major], '0') AS INT) > 10)

	GROUP BY
				P.PackageVersion,
				C.ClientCategory,
				CASE
					WHEN	C.ClientCategory = 'Script' THEN 'Scripted Downloads'
					WHEN	C.ClientCategory = 'Browser' THEN 'Browsers'
					WHEN	C.ClientCategory = 'Mobile' THEN 'Browsers (Mobile)'
					WHEN	C.ClientName = 'NuGet' THEN 'NuGet.Core-based Downloads'
					WHEN	C.ClientName = 'NuGet Shim' THEN 'NuGet Client V3'
					ELSE	C.ClientName
				END,
				CASE
					WHEN	C.ClientCategory IN ('Script', 'Browser', 'Mobile') THEN '0'
					ELSE	C.Major
				END,
				CASE
					WHEN	C.ClientCategory IN ('Script', 'Browser', 'Mobile') THEN '0'
					ELSE	C.Minor
				END,
				O.Operation

	ORDER BY
				P.PackageVersion,
				ClientName,
				C.ClientCategory,
				Major,
				Minor,
				O.Operation,
				[Downloads] DESC
END