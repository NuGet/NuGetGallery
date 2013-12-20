CREATE VIEW [work].[InstanceStatistics] AS 
	WITH 
		statuscte AS (
			SELECT [UpdatedBy], [1] AS 'Queued', [2] AS 'Dequeued', [3] AS 'Executing', [4] AS 'Executed', [5] AS 'Cancelled', [6] AS 'Suspended'
			FROM (SELECT [UpdatedBy], [Status] FROM work.Invocations) AS StatusGroups
			PIVOT (
				COUNT([Status]) FOR [Status] IN ([1], [2], [3], [4], [5], [6])
			) AS PivotTable),
		resultcte AS (
			SELECT [UpdatedBy], [1] AS 'Completed', [2] AS 'Faulted', [3] AS 'Crashed', [4] AS 'Aborted'
			FROM (SELECT [UpdatedBy], [Result] FROM work.Invocations) AS ResultGroups
			PIVOT (
				COUNT([Result]) FOR [Result] IN ([1], [2], [3], [4])
			) AS PivotTable),
		countcte AS (
			SELECT [UpdatedBy], COUNT(*) AS [Total] FROM work.Invocations GROUP BY [UpdatedBy])
	SELECT s.[UpdatedBy] AS 'Item', [Queued], [Dequeued], [Executing], [Executed], [Cancelled], [Suspended], [Completed], [Faulted], [Crashed], [Aborted], [Total]
	FROM statuscte s
	INNER JOIN resultcte r ON s.[UpdatedBy] = r.[UpdatedBy]
	INNER JOIN countcte c ON r.[UpdatedBy] = c.[UpdatedBy]
