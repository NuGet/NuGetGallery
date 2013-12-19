CREATE PROCEDURE [dbo].[GetHiddenInvocations]
AS
	SELECT * 
	FROM Invocations 
	WHERE NextVisibleAt > SYSDATETIMEOFFSET()