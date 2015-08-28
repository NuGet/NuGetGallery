CREATE PROCEDURE [dbo].[DownloadReportListInactive]
AS
BEGIN
	SET NOCOUNT ON;

		SELECT	DISTINCT [PackageId]
		FROM	[dbo].[Fact_Download] AS F

		-- INNER JOIN with Downloads to ensure we do not remove newly inserted package id's
		INNER JOIN	Dimension_Package AS P
		ON			P.[Id] = F.[Dimension_Package_Id]


	EXCEPT

		-- Find all packages that have been downloaded in the last 42 days
		SELECT	DISTINCT P.[PackageId]
		FROM	[dbo].[Fact_Download] (NOLOCK) AS F

		INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
		ON			P.[Id] = F.[Dimension_Package_Id]

		WHERE	ISNULL(F.[Timestamp], CONVERT(DATETIME, '1900-01-01')) > CONVERT(DATE, DATEADD(day, -42, GETDATE()))
END