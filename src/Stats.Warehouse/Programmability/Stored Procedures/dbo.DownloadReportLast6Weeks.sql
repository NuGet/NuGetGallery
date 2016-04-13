CREATE PROCEDURE [dbo].[DownloadReportLast6Weeks]
	@ReportGenerationTime DATETIME
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @MinDate DATE
	DECLARE @MinWeekOfYear INT
	DECLARE @MinYear INT

	SELECT	@MinWeekOfYear = [WeekOfYear],
			@MinYear = [Year]
	FROM	[dbo].[Dimension_Date] (NOLOCK)
	WHERE	[Date] = CAST(DATEADD(day, -42, @ReportGenerationTime) AS DATE)

	SELECT	@MinDate = MIN([Date])
	FROM	[dbo].[Dimension_Date] (NOLOCK)
	WHERE	[WeekOfYear] = @MinWeekOfYear
		AND	[Year] = @MinYear

	DECLARE @Cursor DATETIME = (SELECT ISNULL(MAX([Position]), @ReportGenerationTime) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')

	SELECT	TOP 6 D.[Year],
			D.[WeekOfYear],
			SUM(ISNULL(Facts.[DownloadCount], 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			D.[Id] = Facts.[Dimension_Date_Id]

	WHERE	D.[Date] IS NOT NULL
			AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >=
				DATETIMEFROMPARTS(
					DATEPART(year, @MinDate),
					DATEPART(month, @MinDate),
					DATEPART(day, @MinDate),
					0, 0, 0, 0)
			AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, @ReportGenerationTime))) <
				DATETIMEFROMPARTS(
					DATEPART(year, @ReportGenerationTime),
					DATEPART(month, @ReportGenerationTime),
					DATEPART(day, @ReportGenerationTime),
					0, 0, 0, 0)
			AND Facts.[Timestamp] <= @Cursor

	GROUP BY	D.[Year], D.[WeekOfYear]
	ORDER BY	[Year], [WeekOfYear]

END