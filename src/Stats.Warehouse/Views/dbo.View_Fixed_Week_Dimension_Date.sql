CREATE VIEW [dbo].View_Fixed_Week_Dimension_Date
	WITH SCHEMABINDING
AS
	-- [Dimension_Date] table have [WeekOfYear] and [Year] columns that are set to the
	-- number of the week in a year and a year respectively.
	-- This might lead to a situation around a new year day that within single week,
	-- there are days that have different values of [WeekOfYear] and [Year]:
	-- 12/30/2018 -> 53rd of 2018 [Sunday]
	-- 12/31/2018 -> 53rd of 2018 [Monday]
	-- 1/1/2019 -> 1st of 2019 [Tuesday]
	-- 1/2/2019 -> 1st of 2019 [Wednesday]
	-- etc.
	-- which results in the wrong grouping when group by [WeekOfYear] and [Year] is done:
	-- the single week gets split into two portions, one for the previous year and another for
	-- the new one with aggregations calculated separately for each of the portions.
	-- This view works around the issue by making sure that all days of the week map to
	-- the same [WeekOfYear] and [Year], specifically to that of the first day of that week.
	SELECT d.[Id], d.[Date], dd.WeekOfYear, dd.[Year]
	FROM [dbo].Dimension_Date AS d WITH(NOLOCK)
	CROSS APPLY
	(
		SELECT TOP(1) [WeekOfYear], [Year]
		FROM [dbo].[Dimension_Date] AS d2 WITH(NOLOCK)
		WHERE d2.[Date] IS NOT NULL AND d.[Date] IS NOT NULL AND d2.[DayOfWeek] IS NOT NULL
			AND ISNULL(d2.[Date], '1980-01-01') <= ISNULL(d.[Date], '1980-01-01') AND ISNULL(d2.[DayOfWeek], 0) = 1
		ORDER BY d2.[Date] DESC
	) AS dd