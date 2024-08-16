CREATE PROCEDURE [dbo].[GetTotalPackageDownloadsByDate]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	D.[Date],
			SUM(A.[PackageDownloads])
	FROM [dbo].[Agg_PackageDownloads_LogFile] AS A (NOLOCK)
	INNER JOIN [dbo].[Dimension_Date] AS D (NOLOCK)
	ON	D.[Id] = A.[Dimension_Date_Id]
	GROUP BY D.[Date]
	ORDER BY [Date] DESC
END