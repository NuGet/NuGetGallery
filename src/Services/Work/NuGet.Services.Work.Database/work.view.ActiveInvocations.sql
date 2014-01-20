-- View that shows the latest version of all currently active invocations
CREATE VIEW [work].[ActiveInvocations] AS 
	WITH cte AS (
		SELECT *, ROW_NUMBER() OVER (PARTITION BY Id ORDER BY [Version] DESC) AS RowNumber
		FROM [private].InvocationsStore
	)
	SELECT * 
    FROM cte 
    WHERE RowNumber = 1 
    AND Complete = 0
