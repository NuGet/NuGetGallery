CREATE PROCEDURE [dbo].[DownloadReportRecentPopularityDetailByPackage]
	@PackageId NVARCHAR(128)
AS
BEGIN
	-- Find all packages that have had download facts added in the last 42 days
	SET NOCOUNT ON;

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

	INNER JOIN	Dimension_Operation AS O (NOLOCK)
	ON			O.[Id] = F.[Dimension_Operation_Id]

	INNER JOIN	Dimension_Client AS C (NOLOCK)
	ON			C.[Id] = F.[Dimension_Client_Id]

	WHERE		ISNULL(F.[Timestamp], CONVERT(DATETIME, '1900-01-01')) > CONVERT(DATE, DATEADD(day, -42, GETDATE()))
			AND P.PackageId = @PackageId
			AND C.ClientCategory NOT IN ('Crawler', 'Browser', 'Unknown')

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