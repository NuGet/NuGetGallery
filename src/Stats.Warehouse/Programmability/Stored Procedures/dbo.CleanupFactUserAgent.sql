CREATE PROCEDURE [dbo].[CleanupFactUserAgent]
AS
BEGIN
	DELETE
	FROM	[dbo].[Fact_UserAgent]
	WHERE	[Id] NOT IN	(
						SELECT	DISTINCT [Fact_UserAgent_Id]
						FROM	[dbo].[Fact_Download] (NOLOCK)
						)
		AND [Id] NOT IN (
						SELECT	DISTINCT [Fact_UserAgent_Id]
						FROM	[dbo].[Fact_Dist_Download] (NOLOCK)
						)
END