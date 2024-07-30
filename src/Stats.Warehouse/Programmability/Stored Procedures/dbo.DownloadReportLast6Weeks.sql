CREATE PROCEDURE [dbo].[DownloadReportLast6Weeks]
	@ReportGenerationTime DATETIME
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @6WeeksAgo DATE = CAST(DATEADD(WEEK, -6, @ReportGenerationTime) AS DATE)
	DECLARE @MinDate DATE

	SELECT @MinDate = MIN(d1.Date)
	FROM [dbo].View_Fixed_Week_Dimension_Date d1
	JOIN [dbo].View_Fixed_Week_Dimension_Date d2 ON d2.[Date] = @6WeeksAgo AND d2.[WeekOfYear] = d1.[WeekOfYear] AND d2.[Year] = d1.[Year]
	WHERE d1.[Date] >= DATEADD(WEEK, -1, @6WeeksAgo) AND d1.[Date] <= @6WeeksAgo

	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')
	DECLARE @MaxDate DATE = DATEADD(WEEK, 6, @MinDate);

	SELECT	D.[Year],
			D.[WeekOfYear],
			SUM(CAST(ISNULL(Facts.[DownloadCount], 0) AS BIGINT)) AS [Downloads]
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)
	INNER JOIN [dbo].View_Fixed_Week_Dimension_Date AS D ON D.Id = Facts.Dimension_Date_Id AND D.[Date] >= @MinDate AND D.[Date] < @MaxDate
	GROUP BY D.[Year], D.[WeekOfYear]
	ORDER BY [Year], [WeekOfYear]
END