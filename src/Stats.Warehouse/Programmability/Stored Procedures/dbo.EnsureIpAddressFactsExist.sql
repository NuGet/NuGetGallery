CREATE PROCEDURE [dbo].[EnsureIpAddressFactsExist]
	@addresses [dbo].[IpAddressFactTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[Address]			VARBINARY(16)	NOT NULL,
		[TextAddress]		NVARCHAR(45)	NOT NULL,
		INDEX IX_Results NONCLUSTERED ([Id], [Address], [TextAddress])
	)

	DECLARE @Address VARBINARY(16)
	DECLARE @TextAddress NVARCHAR(45)

	BEGIN TRY

		-- Open Cursor
		DECLARE ipaddress_Cursor CURSOR FOR
			SELECT	[Address], [TextAddress]
			FROM	@addresses

		OPEN	ipaddress_Cursor FETCH NEXT
		FROM	ipaddress_Cursor
		INTO	@Address, @TextAddress

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Create fact if not exists
			IF NOT EXISTS (SELECT Id FROM [Fact_IpAddress] (NOLOCK) WHERE ISNULL([TextAddress], '') = @TextAddress)
				INSERT INTO [Fact_IpAddress] ([Address], [TextAddress])
					OUTPUT inserted.Id, @Address, @TextAddress INTO @results
				VALUES (@Address, @TextAddress);
			ELSE
				INSERT INTO @results ([Id], [Address], [TextAddress])
				SELECT	[Id], @Address, @TextAddress
				FROM	[dbo].[Fact_IpAddress] (NOLOCK)
				WHERE	ISNULL([TextAddress], '') = @TextAddress

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM ipaddress_Cursor
			INTO @Address, @TextAddress
		END

		-- Close cursor
		CLOSE ipaddress_Cursor
		DEALLOCATE ipaddress_Cursor

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [TextAddress]
	FROM		@results

END