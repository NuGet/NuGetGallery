CREATE FUNCTION [dbo].[IsCrawler]
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
			-- Bots / Crawlers (filtered in the reports)
			CHARINDEX('Bot', @ClientName) > 0
		OR	CHARINDEX('bot', @ClientName) > 0
		OR	CHARINDEX('Slurp', @ClientName) > 0
		OR	CHARINDEX('BingPreview', @ClientName) > 0
		OR	CHARINDEX('crawler', @ClientName) > 0
		OR	CHARINDEX('sniffer', @ClientName) > 0
		OR	CHARINDEX('spider', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END