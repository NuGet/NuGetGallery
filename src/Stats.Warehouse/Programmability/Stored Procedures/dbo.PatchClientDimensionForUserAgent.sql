CREATE PROCEDURE [dbo].[PatchClientDimensionForUserAgent]
	@NewClientDimensionId INT = 1,
	@UserAgent NVARCHAR(500)
AS
BEGIN
	SET NOCOUNT ON;


	IF @UserAgent IS NOT NULL
	BEGIN

		UPDATE	[dbo].[Fact_Download]
		SET		[Dimension_Client_Id] = @NewClientDimensionId
		WHERE	[UserAgent] IS NOT NULL
				AND ISNULL([UserAgent], '') = @UserAgent
				AND [Dimension_Client_Id] = 1
	END

END