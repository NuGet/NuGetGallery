CREATE PROCEDURE [dbo].[EnsurePackageDimensionsExist]
	@packages [dbo].[PackageDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[PackageId]         NVARCHAR(128)	NULL,
		[PackageVersion]	NVARCHAR(64)	NULL
	)

	DECLARE @PackageId VARCHAR(128)
	DECLARE @PackageVersion NVARCHAR(64)

	BEGIN TRY
		SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
		BEGIN TRANSACTION

		-- Open Cursor
		DECLARE package_Cursor CURSOR FOR
			SELECT	[PackageId], [PackageVersion]
			FROM	@packages

		OPEN	package_Cursor FETCH NEXT
		FROM	package_Cursor
		INTO	@packageId, @packageVersion

		WHILE @@FETCH_STATUS = 0
		BEGIN

			-- Create dimension if not exists
			IF NOT EXISTS (SELECT Id FROM [Dimension_Package] WHERE ISNULL([PackageId], '') = @PackageId AND ISNULL([PackageVersion], '') = @PackageVersion)
				INSERT INTO [Dimension_Package] ([PackageId], [PackageVersion])
					OUTPUT inserted.Id, inserted.PackageId, inserted.PackageVersion INTO @results
				VALUES (@PackageId, @PackageVersion);
			ELSE
				INSERT INTO @results ([Id], [PackageId], [PackageVersion])
				SELECT	[Id], [PackageId], [PackageVersion]
				FROM	[dbo].[Dimension_Package]
				WHERE	ISNULL([PackageId], '') = @PackageId
						AND ISNULL([PackageVersion], '') = @PackageVersion

			-- Advance cursor
			FETCH NEXT FROM package_Cursor
			INTO @packageId, @packageVersion
		END

		-- Close cursor
		CLOSE package_Cursor
		DEALLOCATE package_Cursor

		COMMIT

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

	-- Select all matching dimensions
	SELECT		[Id], [PackageId], [PackageVersion]
	FROM		@results

END