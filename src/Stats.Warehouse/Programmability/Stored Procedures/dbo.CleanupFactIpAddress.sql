CREATE PROCEDURE [dbo].[CleanupFactIpAddress]
AS
BEGIN
	DELETE
	FROM	[dbo].[Fact_IpAddress]
	WHERE	[Id] NOT IN	(
						SELECT	DISTINCT [Fact_EdgeServer_IpAddress_Id]
						FROM	[dbo].[Fact_Download] (NOLOCK)
						)
		AND [Id] NOT IN (
						SELECT	DISTINCT [Fact_EdgeServer_IpAddress_Id]
						FROM	[dbo].[Fact_Dist_Download] (NOLOCK)
						)
END