CREATE FUNCTION [dbo].[IsMobileClient]
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
			CHARINDEX('Mobile', @ClientName) > 0
		OR	CHARINDEX('Android', @ClientName) > 0
		OR	CHARINDEX('Kindle', @ClientName) > 0
		OR	CHARINDEX('BlackBerry', @ClientName) > 0
		OR	CHARINDEX('Openwave', @ClientName) > 0
		OR	CHARINDEX('NetFront', @ClientName) > 0
		OR	CHARINDEX('CFNetwork', @ClientName) > 0
		OR	CHARINDEX('iLunascape', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END