CREATE PROCEDURE [dbo].[EnsureClientDimensionsExist]
	@clients [dbo].[ClientDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[UserAgent]         VARCHAR(MAX)	NULL
	)

	DECLARE @UserAgent VARCHAR(MAX)
	DECLARE @ClientName VARCHAR(128)
	DECLARE @Major VARCHAR(50)
	DECLARE @Minor VARCHAR(50)
	DECLARE @Patch VARCHAR(50)

	-- Open Cursor
	DECLARE client_Cursor CURSOR FOR
		SELECT	[UserAgent], [ClientName], [Major], [Minor], [Patch]
		FROM	@clients

	OPEN	client_Cursor FETCH NEXT
	FROM	client_Cursor
	INTO	@UserAgent, @ClientName, @Major, @Minor, @Patch

	WHILE @@FETCH_STATUS = 0
	BEGIN

		-- Create dimension if not exists
		IF NOT EXISTS (SELECT Id FROM [Dimension_Client] WHERE [ClientName] = @ClientName AND [Major] = @Major AND [Minor] = @Minor AND [Patch] = @Patch)
			INSERT INTO [Dimension_Client] ([ClientName], [Major], [Minor], [Patch])
				OUTPUT inserted.Id, @UserAgent INTO @results
			VALUES (@ClientName, @Major, @Minor, @Patch);
		ELSE
			INSERT INTO @results ([Id], [UserAgent])
			SELECT	[Id], @UserAgent
			FROM	[dbo].[Dimension_Client]
			WHERE	[ClientName] = @ClientName
					AND [Major] = @Major
					AND [Minor] = @Minor
					AND [Patch] = @Patch

		-- Advance cursor
		FETCH NEXT FROM client_Cursor
		INTO @UserAgent, @ClientName, @Major, @Minor, @Patch
	END

	-- Close cursor
	CLOSE client_Cursor
	DEALLOCATE client_Cursor

	-- Select all matching dimensions
	SELECT		[Id], [UserAgent]
	FROM		@results

END