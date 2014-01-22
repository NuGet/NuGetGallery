-- View that shows the latest version of each Job Instance
CREATE VIEW [work].[InvocationsByJobInstance] AS 
	WITH cte AS (
    	SELECT *, ROW_NUMBER() OVER (PARTITION BY JobInstanceName ORDER BY [Version] DESC) AS RowNumber
    	FROM [private].InvocationsStore
    )
    SELECT * 
    FROM cte 
    WHERE RowNumber = 1
