CREATE FUNCTION [dbo].[IsScriptClient]
(
	@ClientName NVARCHAR(128)
)
RETURNS BIT
WITH SCHEMABINDING
AS
BEGIN
	IF @ClientName IS NULL
		RETURN 0

	IF	(
			CHARINDEX('PowerShell', @ClientName) > 0
		OR	CHARINDEX('curl', @ClientName) > 0
		OR	CHARINDEX('Wget', @ClientName) > 0
		OR	CHARINDEX('Java', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END