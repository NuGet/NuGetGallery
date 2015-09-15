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
			C.ClientName,
			C.Major,
			C.Minor,
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
			AND C.ClientCategory NOT IN ('Crawler', 'Script', 'Unknown')
			AND NOT (C.ClientCategory = 'NuGet' AND ISNULL(C.Major, '0') = '99')

	GROUP BY
				P.PackageVersion,
				C.ClientName,
				C.ClientCategory,
				C.Major,
				C.Minor,
				O.Operation

	ORDER BY
				P.PackageVersion,
				C.ClientName,
				C.ClientCategory,
				C.Major,
				C.Minor,
				O.Operation,
				[Downloads] DESC
END