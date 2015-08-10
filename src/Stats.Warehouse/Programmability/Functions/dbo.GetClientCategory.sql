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
			CHARINDEX('Mobile', @ClientName) > 0
		OR	CHARINDEX('Android', @ClientName) > 0
		OR	CHARINDEX('Kindle', @ClientName) > 0
		OR	CHARINDEX('BlackBerry', @ClientName) > 0
		OR	CHARINDEX('Openwave', @ClientName) > 0
		OR	CHARINDEX('NetFront', @ClientName) > 0
		OR	CHARINDEX('CFNetwork', @ClientName) > 0
		OR	CHARINDEX('iLunascape', @ClientName) > 0
		)
		RETURN 'Mobile'

    IF (
			-- Browsers
			CHARINDEX('Mozilla', @ClientName) > 0
		OR	CHARINDEX('Firefox', @ClientName) > 0
		OR	CHARINDEX('Opera', @ClientName) > 0
		OR	CHARINDEX('Chrome', @ClientName) > 0
		OR	CHARINDEX('Chromium', @ClientName) > 0
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
		OR	CHARINDEX('AppleMail', @ClientName) > 0
		OR	CHARINDEX('Lunascape', @ClientName) > 0
		)
        RETURN 'Browser'

	IF (
			-- Bots / Crawlers (filtered in the reports)
			CHARINDEX('Bot', @ClientName) > 0
		OR	CHARINDEX('bot', @ClientName) > 0
		OR	CHARINDEX('Slurp', @ClientName) > 0
		OR	CHARINDEX('BingPreview', @ClientName) > 0
		)
        RETURN 'Crawler'

	IF (
			-- explicitly categorize unknowns, test frameworks or others that should be filtered out in the reports
			CHARINDEX('PhantomJS', @ClientName) > 0
		OR	CHARINDEX('WebKit Nightly', @ClientName) > 0
		OR	CHARINDEX('Python Requests', @ClientName) > 0
		OR	CHARINDEX('Jasmine', @ClientName) > 0
		)
		RETURN 'Unknown'

	-- Return empty for all others to allow ecosystem user agents to be picked up in the reports
    RETURN ''
END