CREATE PROCEDURE [dbo].[GetLinkedUserAgents]
	@TargetClientName VARCHAR (128) NULL,
	@UserAgentFilter VARCHAR(128) = NULL
AS
BEGIN

	IF @TargetClientName IS NULL
		RETURN;

	SET NOCOUNT ON;

	DECLARE @tblClientDimensionIds AS TABLE
	(
		[Id] INT
		INDEX IX_ClientDimensionIds NONCLUSTERED ([Id])
	)

	INSERT INTO		@tblClientDimensionIds
	SELECT	[Id]
	FROM	[dbo].[Dimension_Client] AS C (NOLOCK)
	WHERE	C.[ClientName] IS NOT NULL
			AND ISNULL(C.[ClientName], '') = @TargetClientName

	SELECT	DISTINCT UA.[UserAgent], UA.[Id], C.[Id]
	FROM	[dbo].[Fact_UserAgent] AS UA (NOLOCK)

	INNER JOIN	[dbo].[Fact_Download] (NOLOCK) AS F
	ON			UA.[Id] = F.[Fact_UserAgent_Id]

	INNER JOIN	@tblClientDimensionIds AS C
	ON			C.[Id] = F.[Dimension_Client_Id]

	WHERE	(@UserAgentFilter IS NULL OR ISNULL(UA.[UserAgent], '') NOT LIKE @UserAgentFilter)

END