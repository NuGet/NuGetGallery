CREATE VIEW [jobs].[JobStatistics] AS
	WITH 
		statuscte AS (
			SELECT [Job], [1] AS 'Queued', [2] AS 'Dequeued', [3] AS 'Executing', [4] AS 'Executed', [5] AS 'Cancelled', [6] AS 'Suspended'
			FROM (SELECT [Job], [Status] FROM jobs.Invocations) AS StatusGroups
			PIVOT (
				COUNT([Status]) FOR [Status] IN ([1], [2], [3], [4], [5], [6])
			) AS PivotTable),
		resultcte AS (
			SELECT [Job], [1] AS 'Completed', [2] AS 'Faulted', [3] AS 'Crashed', [4] AS 'Aborted'
			FROM (SELECT [Job], [Result] FROM jobs.Invocations) AS ResultGroups
			PIVOT (
				COUNT([Result]) FOR [Result] IN ([1], [2], [3], [4])
			) AS PivotTable),
		countcte AS (
			SELECT [Job], COUNT(*) AS [Total] FROM jobs.Invocations GROUP BY [Job])
	SELECT s.[Job] AS 'Item', [Queued], [Dequeued], [Executing], [Executed], [Cancelled], [Suspended], [Completed], [Faulted], [Crashed], [Aborted], [Total]
	FROM statuscte s
	INNER JOIN resultcte r ON s.[Job] = r.[Job]
	INNER JOIN countcte c ON r.[Job] = c.[Job]
