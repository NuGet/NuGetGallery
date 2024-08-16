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

    IF	([dbo].[IsNuGetClient] (@ClientName) = 1)
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

	IF	([dbo].[IsScriptClient] (@ClientName) = 1)
		RETURN 'Script'

	IF	([dbo].[IsCrawler] (@ClientName) = 1)
        RETURN 'Crawler'

	IF ([dbo].[IsMobileClient] (@ClientName) = 1)
		RETURN 'Mobile'

	-- Check these late in the process, because other User Agents tend to also send browser strings (e.g. PowerShell sends the Mozilla string along)
    IF ([dbo].[IsBrowser] (@ClientName) = 1)
        RETURN 'Browser'

	-- explicitly categorize unknowns, test frameworks or others that should be filtered out in the reports
	IF ([dbo].[IsUnknownClient] (@ClientName) = 1)
		RETURN 'Unknown'

	-- Return empty for all others to allow ecosystem user agents to be picked up in the reports
    RETURN ''
END