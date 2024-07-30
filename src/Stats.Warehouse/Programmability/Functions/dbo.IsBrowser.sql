CREATE FUNCTION [dbo].[IsBrowser]
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
			-- Browsers
			CHARINDEX('Mozilla', @ClientName) > 0
		OR	CHARINDEX('Firefox', @ClientName) > 0
		OR	CHARINDEX('Opera', @ClientName) > 0
		OR	CHARINDEX('Chrome', @ClientName) > 0
		OR	CHARINDEX('Chromium', @ClientName) > 0
		OR	CHARINDEX('Internet Explorer', @ClientName) > 0
		OR	CHARINDEX('Browser', @ClientName) > 0
		OR	@ClientName = 'IE'
		OR	@ClientName = 'Iron'
		OR	CHARINDEX('Safari', @ClientName) > 0
		OR	CHARINDEX('Sogou Explorer', @ClientName) > 0
		OR	CHARINDEX('Maxthon', @ClientName) > 0
		OR	CHARINDEX('SeaMonkey', @ClientName) > 0
		OR	CHARINDEX('Iceweasel', @ClientName) > 0
		OR	CHARINDEX('Sleipnir', @ClientName) > 0
		OR	CHARINDEX('Konqueror', @ClientName) > 0
		OR	CHARINDEX('Lynx', @ClientName) > 0
		OR	CHARINDEX('Galeon', @ClientName) > 0
		OR	CHARINDEX('Epiphany', @ClientName) > 0
		OR	CHARINDEX('Lunascape', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END