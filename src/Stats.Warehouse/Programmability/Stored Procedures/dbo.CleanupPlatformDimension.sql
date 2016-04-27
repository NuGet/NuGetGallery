CREATE PROCEDURE [dbo].[CleanupPlatformDimension]
AS
BEGIN
	DECLARE @PlatformId TABLE
	(
		[Id] INT NULL
	)

	INSERT INTO @PlatformId
	SELECT	DISTINCT P.[Id]
	FROM	[dbo].[Dimension_Platform] AS P (NOLOCK)
	INNER JOIN [dbo].[Fact_Download] AS F (NOLOCK)
	ON	P.[Id] = F.[Dimension_Platform_Id]

	INSERT INTO @PlatformId
	SELECT	DISTINCT P.[Id]
	FROM	[dbo].[Dimension_Platform] AS P (NOLOCK)
	INNER JOIN [dbo].[Fact_Dist_Download] AS F (NOLOCK)
	ON	P.[Id] = F.[Dimension_Platform_Id]

	DELETE
	FROM	[dbo].[Dimension_Platform]
	WHERE	[Id] NOT IN (SELECT DISTINCT [Id] FROM @PlatformId)
END