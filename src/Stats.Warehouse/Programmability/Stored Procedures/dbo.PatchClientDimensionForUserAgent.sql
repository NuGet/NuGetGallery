CREATE PROCEDURE [dbo].[PatchClientDimensionForUserAgent]
	@NewClientDimensionId INT = 1,
	@UserAgentId INT
AS
BEGIN
	SET NOCOUNT ON;


	IF @UserAgentId IS NOT NULL
	BEGIN

		UPDATE	[dbo].[Fact_Download]
		SET		[Dimension_Client_Id] = @NewClientDimensionId
		WHERE	[Fact_UserAgent_Id] IS NOT NULL
				AND [Fact_UserAgent_Id] = @UserAgentId
				AND [Dimension_Client_Id] = 1
	END

END