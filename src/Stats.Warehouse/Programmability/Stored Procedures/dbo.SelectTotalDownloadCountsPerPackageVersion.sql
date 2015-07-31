CREATE PROCEDURE [dbo].[SelectTotalDownloadCountsPerPackageVersion]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	[PackageId],
			[PackageVersion],
			SUM([TotalDownloadCount]) AS [TotalDownloadCount]
	FROM	[dbo].[vwAggregatedDownloads]
	GROUP BY	[PackageId],
				[PackageVersion]

END