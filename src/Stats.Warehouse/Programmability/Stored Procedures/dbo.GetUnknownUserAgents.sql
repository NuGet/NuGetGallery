CREATE PROCEDURE [dbo].[GetUnknownUserAgents]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	DISTINCT UA.[UserAgent]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F

	INNER JOIN	[dbo].[Fact_UserAgent] AS UA (NOLOCK)
	ON			UA.[Id] = F.[Fact_UserAgent_Id]

	WHERE	F.[Dimension_Client_Id] = 1 -- the (unknown) client
		AND ISNULL(UA.[UserAgent], '') <> ''
END