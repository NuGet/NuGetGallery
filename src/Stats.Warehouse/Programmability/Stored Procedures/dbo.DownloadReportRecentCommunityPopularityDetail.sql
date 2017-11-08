CREATE PROCEDURE [dbo].[DownloadReportRecentCommunityPopularityDetail]
	@ReportGenerationTime DATETIME
AS
BEGIN
	SET NOCOUNT ON;

	-- Find all non-community package ids that should be filtered out from the report
	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')
	DECLARE @NonCommunityPackagesId int = (SELECT [Id] FROM [dbo].[Dimension_PackageSet] WHERE [Name] = 'NonCommunityPackages');

	-- Find all packages that have had download facts added in the last 42 days, today inclusive
	SELECT		TOP 500
				P.[PackageId]
				,P.[PackageVersion]
				,SUM(ISNULL(F.[DownloadCount], 0)) AS 'Downloads'
	FROM		[dbo].[Fact_Download] AS F (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			F.[Dimension_Date_Id] = D.[Id]

	INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
	ON			F.[Dimension_Package_Id] = P.[Id]

	INNER JOIN	Dimension_Client AS C (NOLOCK)
	ON			C.[Id] = F.[Dimension_Client_Id]

	WHERE		D.[Date] IS NOT NULL
			AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >= CONVERT(DATE, DATEADD(day, -42, @ReportGenerationTime))
			AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, @ReportGenerationTime))) <= CONVERT(DATE, @ReportGenerationTime)
			AND F.[Timestamp] <= @Cursor
			AND C.ClientCategory NOT IN ('Crawler', 'Unknown')
			AND NOT (C.ClientCategory = 'NuGet' AND CAST(ISNULL(C.[Major], '0') AS INT) > 10)
			AND P.[LowercasedPackageId] NOT IN (
				SELECT [LowercasedPackageId]
				FROM [dbo].[Fact_Package_PackageSet]
				WHERE [Dimension_PackageSet_Id] = @NonCommunityPackagesId
			)

	GROUP BY	P.[PackageId],
				P.[PackageVersion]
	ORDER BY	[Downloads] DESC
END