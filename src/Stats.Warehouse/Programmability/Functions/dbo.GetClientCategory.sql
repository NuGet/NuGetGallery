CREATE FUNCTION [dbo].[GetClientCategory]
(
	@ClientName NVARCHAR(128)
)
RETURNS NVARCHAR(50)
WITH SCHEMABINDING
AS
BEGIN
	IF @ClientName IS NULL
		RETURN ''

    IF	(
			-- VS NuGet 2.8+
			CHARINDEX('NuGet VS PowerShell Console', @ClientName) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog - Solution', @ClientName) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog', @ClientName) > 0

			-- VS NuGet (pre-2.8)
		OR	CHARINDEX('NuGet Add Package Dialog', @ClientName) > 0
        OR	CHARINDEX('NuGet Command Line', @ClientName) > 0
        OR	CHARINDEX('NuGet Package Manager Console', @ClientName) > 0
        OR	CHARINDEX('NuGet Visual Studio Extension', @ClientName) > 0
        OR	CHARINDEX('Package-Installer', @ClientName) > 0
		)
        RETURN 'NuGet'

		-- WebMatrix
    IF	CHARINDEX('WebMatrix', @ClientName) > 0
        RETURN 'WebMatrix'

	IF	(
			-- NuGet Package Explorer
			CHARINDEX('NuGet Package Explorer Metro', @ClientName) > 0
		OR	CHARINDEX('NuGet Package Explorer', @ClientName) > 0
		)
		RETURN 'NuGet Package Explorer'

    IF (
			-- Browsers
			CHARINDEX('Mozilla', @ClientName) > 0
		OR	CHARINDEX('Firefox', @ClientName) > 0
		OR	CHARINDEX('Opera', @ClientName) > 0
		OR	CHARINDEX('Chrome', @ClientName) > 0
		OR	CHARINDEX('Browser', @ClientName) > 0
		OR	@ClientName = 'IE'
		OR	CHARINDEX('IE Mobile', @ClientName) > 0
		)
        RETURN 'Browser'

	IF (
			-- Bots
			CHARINDEX('Bot', @ClientName) > 0
		OR	CHARINDEX('bot', @ClientName) > 0
		)
        RETURN 'Bot'

    RETURN ''
END