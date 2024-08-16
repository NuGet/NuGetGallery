CREATE PROCEDURE [dbo].[EnsurePlatformDimensionsExist]
	@platforms [dbo].[PlatformDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[UserAgent]         NVARCHAR(MAX)	NULL
	)

	DECLARE @UserAgent NVARCHAR(MAX)
	DECLARE @OSFamily VARCHAR(128)
	DECLARE @Major VARCHAR(50)
	DECLARE @Minor VARCHAR(50)
	DECLARE @Patch VARCHAR(50)
	DECLARE @PatchMinor VARCHAR(50)

	BEGIN TRY

		-- Open Cursor
		DECLARE platform_Cursor CURSOR FOR
			SELECT	[UserAgent], [OSFamily], [Major], [Minor], [Patch], [PatchMinor]
			FROM	@platforms

		OPEN	platform_Cursor FETCH NEXT
		FROM	platform_Cursor
		INTO	@UserAgent, @OSFamily, @Major, @Minor, @Patch, @PatchMinor

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Create dimension if not exists
			IF NOT EXISTS (SELECT Id FROM [Dimension_Platform] (NOLOCK) WHERE ISNULL([OSFamily], '') = @OSFamily AND ISNULL([Major], '') = @Major AND ISNULL([Minor], '') = @Minor AND ISNULL([Patch], '') = @Patch AND ISNULL([PatchMinor], '') = @PatchMinor)
				INSERT INTO [Dimension_Platform] ([OSFamily], [Major], [Minor], [Patch], [PatchMinor])
					OUTPUT inserted.Id, @UserAgent INTO @results
				VALUES (@OSFamily, @Major, @Minor, @Patch, @PatchMinor);
			ELSE
				INSERT INTO @results ([Id], [UserAgent])
				SELECT	[Id], @UserAgent
				FROM	[dbo].[Dimension_Platform] (NOLOCK)
				WHERE	ISNULL([OSFamily], '') = @OSFamily
						AND ISNULL([Major], '') = @Major
						AND ISNULL([Minor], '') = @Minor
						AND ISNULL([Patch], '') = @Patch
						AND ISNULL([PatchMinor], '') = @PatchMinor

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM platform_Cursor
			INTO @UserAgent, @OSFamily, @Major, @Minor, @Patch, @PatchMinor
		END

		-- Close cursor
		CLOSE platform_Cursor
		DEALLOCATE platform_Cursor

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