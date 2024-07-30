CREATE PROCEDURE [dbo].[SelectAlreadyAggregatedLogFiles]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	DISTINCT [LogFileName]
	FROM	[dbo].[Agg_PackageDownloads_LogFile] (NOLOCK)
	ORDER BY [LogFileName] ASC

END