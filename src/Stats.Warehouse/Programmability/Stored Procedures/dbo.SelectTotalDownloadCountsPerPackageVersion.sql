CREATE PROCEDURE [dbo].[SelectTotalDownloadCountsPerPackageVersion]
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @ReportGenerationTime DATETIME = GETUTCDATE()
	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')

	SELECT	P.[PackageId],
			P.[PackageVersion],
			SUM(ISNULL(F.[DownloadCount], 0)) AS [TotalDownloadCount]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F

	INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
	ON		P.[Id] = F.[Dimension_Package_Id]

	INNER JOIN	Dimension_Client AS C (NOLOCK)
	ON			C.[Id] = F.[Dimension_Client_Id]

	WHERE		(F.[Timestamp] <= @Cursor OR F.[Dimension_Date_Id] = -1)
			AND C.ClientCategory NOT IN ('Crawler', 'Unknown')
			AND NOT (C.ClientCategory = 'NuGet' AND CAST(ISNULL(C.[Major], '0') AS INT) > 10)

	GROUP BY	P.[PackageId],
				P.[PackageVersion]

END