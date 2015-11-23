CREATE FUNCTION [dbo].[IsUnknownClient]
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
			CHARINDEX('PhantomJS', @ClientName) > 0
		OR	CHARINDEX('WebKit Nightly', @ClientName) > 0
		OR	CHARINDEX('Python Requests', @ClientName) > 0
		OR	CHARINDEX('Jasmine', @ClientName) > 0
		OR	CHARINDEX('Java', @ClientName) > 0
		OR	CHARINDEX('AppleMail', @ClientName) > 0
		OR	CHARINDEX('NuGet Test Client', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END