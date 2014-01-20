CREATE VIEW [work].[InvocationStatistics] AS
	WITH 
		statuscte AS (
			SELECT [1] AS 'Queued', [2] AS 'Dequeued', [3] AS 'Executing', [4] AS 'Executed', [5] AS 'Cancelled', [6] AS 'Suspended'
			FROM (SELECT [Status] FROM work.Invocations) AS StatusGroups
			PIVOT (
				COUNT([Status]) FOR [Status] IN ([1], [2], [3], [4], [5], [6])
			) AS PivotTable),
		resultcte AS (
			SELECT [1] AS 'Completed', [2] AS 'Faulted', [3] AS 'Crashed', [4] AS 'Aborted'
			FROM (SELECT [Result] FROM work.Invocations) AS ResultGroups
			PIVOT (
				COUNT([Result]) FOR [Result] IN ([1], [2], [3], [4])
			) AS PivotTable),
		countcte AS (
			SELECT COUNT(*) AS [Total] FROM work.Invocations)
	SELECT countcte.*, statuscte.*, resultcte.*
	FROM countcte
	CROSS APPLY statuscte
	CROSS APPLY resultcte