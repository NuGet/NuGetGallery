CREATE PROCEDURE [dbo].[DownloadReportLast6Months]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	D.[Year],
			D.[MonthOfYear],
			SUM(ISNULL(Facts.[DownloadCount], 0)) 'Downloads'
	FROM	[dbo].[Fact_Download] AS Facts (NOLOCK)

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			D.[Id] = Facts.[Dimension_Date_Id]

	WHERE	D.[Date] IS NOT NULL
			AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >=
				DATETIMEFROMPARTS(
					DATEPART(year, DATEADD(month, -7, GETDATE())),
					DATEPART(month, DATEADD(month, -7, GETDATE())),
					1, 0, 0, 0, 0)
			AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, GETDATE()))) <
				DATETIMEFROMPARTS(
					DATEPART(year, GETDATE()),
					DATEPART(month, GETDATE()),
					1, 0, 0, 0, 0)
			AND Facts.[Timestamp] <= (SELECT MAX([Position]) FROM [dbo].[Cursors] (NOLOCK) WHERE [Name] = 'GetDirtyPackageId')

	GROUP BY	D.[Year], D.[MonthOfYear]
	ORDER BY	[Year], [MonthOfYear]
END