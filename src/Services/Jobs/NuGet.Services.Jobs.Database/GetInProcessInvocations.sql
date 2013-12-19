CREATE PROCEDURE [dbo].[GetInProcessInvocations]
AS
	SELECT * 
	FROM Invocations 
	WHERE NextVisibleAt > SYSDATETIMEOFFSET() 
		AND Complete = 0
		AND Dequeued = 1
		AND [LastInstanceName] IS NOT NULL