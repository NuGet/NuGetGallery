CREATE PROCEDURE [dbo].[EnsureToolDimensionsExist]
	@tools [dbo].[ToolDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL PRIMARY KEY,
		[ToolId]			NVARCHAR(255)	NOT NULL,
		[ToolVersion]		NVARCHAR(128)	NOT NULL,
		[FileName]			NVARCHAR(128)	NOT NULL,
		INDEX IX_Results NONCLUSTERED ([ToolId], [ToolVersion], [FileName])
	)

	-- Select existing packages and insert them into the results table
	INSERT INTO @results ([Id], [ToolId], [ToolVersion], [FileName])
		SELECT	T.[Id], T.[ToolId], T.[ToolVersion], T.[FileName]
		FROM	[dbo].[Dimension_Tool] AS T (NOLOCK)
		INNER JOIN	@tools AS I
		ON	T.[LowercasedToolId] = LOWER(I.ToolId)
			AND T.[LowercasedToolVersion] = LOWER(I.ToolVersion)
			AND T.[LowercasedFileName] = LOWER(I.FileName)

	-- Insert new packages
	BEGIN TRY
		SET TRANSACTION ISOLATION LEVEL READ COMMITTED
		BEGIN TRANSACTION

			INSERT INTO [Dimension_Tool] ([ToolId], [ToolVersion], [FileName])
				OUTPUT inserted.Id, inserted.ToolId, inserted.ToolVersion, inserted.FileName INTO @results
			SELECT	[ToolId], [ToolVersion], [FileName]
				FROM	@tools
			EXCEPT
			SELECT	[ToolId], [ToolVersion], [FileName]
				FROM	@results

		COMMIT

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [ToolId], [ToolVersion], [FileName]
	FROM		@results

END