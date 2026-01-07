CREATE PROCEDURE [dbo].[DownloadReportListInactive]
	@ReportGenerationTime DATETIME
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')

	SELECT	DISTINCT P.[PackageId]
	FROM	[dbo].[Fact_Download] AS F

	-- INNER JOIN with Downloads to ensure we do not remove newly inserted package id's
	INNER JOIN	Dimension_Package AS P
	ON			P.[Id] = F.[Dimension_Package_Id]

	WHERE P.[PackageId] NOT IN (

		-- Find all packages that have had download facts added in the last 42 days
		SELECT	DISTINCT SP.[PackageId]
		FROM	[dbo].[Fact_Download] (NOLOCK) AS SF

		INNER JOIN	Dimension_Package (NOLOCK) AS SP
		ON			SP.[Id] = SF.[Dimension_Package_Id]

		INNER JOIN	[dbo].[Dimension_Date] (NOLOCK) AS SD
		ON			SD.[Id] = SF.[Dimension_Date_Id]

		WHERE		SD.[Date] IS NOT NULL
				AND ISNULL(SD.[Date], CONVERT(DATE, '1900-01-01')) >= CONVERT(DATE, DATEADD(day, -42, @ReportGenerationTime))
				AND ISNULL(SD.[Date], CONVERT(DATE, DATEADD(day, 1, @ReportGenerationTime))) <= CONVERT(DATE, @ReportGenerationTime)
				AND SF.[Timestamp] <= @Cursor

	)
END