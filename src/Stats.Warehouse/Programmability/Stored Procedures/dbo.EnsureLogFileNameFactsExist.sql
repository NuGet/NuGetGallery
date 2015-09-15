CREATE PROCEDURE [dbo].[EnsureLogFileNameFactsExist]
	@logfilenames [dbo].[LogFileNameFactTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[LogFileName]       NVARCHAR(255)	NULL,
		INDEX IX_Results NONCLUSTERED ([Id], [LogFileName])
	)

	DECLARE @LogFileName NVARCHAR(255)

	BEGIN TRY

		-- Open Cursor
		DECLARE logfilenames_Cursor CURSOR FOR
			SELECT	[LogFileName]
			FROM	@logfilenames

		OPEN	logfilenames_Cursor FETCH NEXT
		FROM	logfilenames_Cursor
		INTO	@LogFileName

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Create fact if not exists
			IF NOT EXISTS (SELECT Id FROM [Fact_LogFileName] (NOLOCK) WHERE ISNULL([LogFileName], '') = @LogFileName)
				INSERT INTO [Fact_LogFileName] ([LogFileName])
					OUTPUT inserted.Id, @LogFileName INTO @results
				VALUES (@LogFileName);
			ELSE
				INSERT INTO @results ([Id], [LogFileName])
				SELECT	[Id], @LogFileName
				FROM	[dbo].[Fact_LogFileName] (NOLOCK)
				WHERE	ISNULL([LogFileName], '') = @LogFileName

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM logfilenames_Cursor
			INTO @LogFileName
		END

		-- Close cursor
		CLOSE logfilenames_Cursor
		DEALLOCATE logfilenames_Cursor

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [LogFileName]
	FROM		@results

END