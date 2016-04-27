CREATE PROCEDURE [dbo].[EnsureUserAgentFactsExist]
	@useragents [dbo].[UserAgentFactTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[UserAgent]         NVARCHAR(2048)	NULL
	)

	DECLARE @UserAgent NVARCHAR(2048)

	BEGIN TRY

		-- Open Cursor
		DECLARE useragent_Cursor CURSOR FOR
			SELECT	[UserAgent]
			FROM	@useragents

		OPEN	useragent_Cursor FETCH NEXT
		FROM	useragent_Cursor
		INTO	@UserAgent

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Create fact if not exists
			IF NOT EXISTS (SELECT Id FROM [Fact_UserAgent] (NOLOCK) WHERE ISNULL([UserAgent], '') = @UserAgent)
				INSERT INTO [Fact_UserAgent] ([UserAgent])
					OUTPUT inserted.Id, @UserAgent INTO @results
				VALUES (@UserAgent);
			ELSE
				INSERT INTO @results ([Id], [UserAgent])
				SELECT	[Id], @UserAgent
				FROM	[dbo].[Fact_UserAgent] (NOLOCK)
				WHERE	ISNULL([UserAgent], '') = @UserAgent

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM useragent_Cursor
			INTO @UserAgent
		END

		-- Close cursor
		CLOSE useragent_Cursor
		DEALLOCATE useragent_Cursor

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [UserAgent]
	FROM		@results

END