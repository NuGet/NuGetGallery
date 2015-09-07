CREATE PROCEDURE [dbo].[GetUnknownUserAgents]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	DISTINCT F.[UserAgent]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F

	WHERE	F.[Dimension_Client_Id] = 1 -- the (unknown) client
		AND ISNULL(F.[UserAgent], '') <> ''
END