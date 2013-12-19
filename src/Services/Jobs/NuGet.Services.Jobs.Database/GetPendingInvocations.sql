CREATE PROCEDURE [dbo].[GetPendingInvocations]
AS
	SELECT * 
	FROM Invocations 
	WHERE NextVisibleAt <= SYSDATETIMEOFFSET()