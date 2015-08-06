CREATE PROCEDURE [dbo].[EnsurePackageDimensionsExist]
	@packages [dbo].[PackageDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL,
		[PackageId]         NVARCHAR(128)	NOT NULL,
		[PackageVersion]	NVARCHAR(128)	NOT NULL
	)

	DECLARE @PackageId VARCHAR(128)
	DECLARE @PackageVersion NVARCHAR(128)

	BEGIN TRY

		-- Open Cursor
		DECLARE package_Cursor CURSOR FOR
			SELECT	[PackageId], [PackageVersion]
			FROM	@packages

		OPEN	package_Cursor FETCH NEXT
		FROM	package_Cursor
		INTO	@packageId, @packageVersion

		WHILE @@FETCH_STATUS = 0
		BEGIN


			DECLARE @packageIdLowercase NVARCHAR(128) = LOWER(@packageId)
			DECLARE @packageVersionLowercase NVARCHAR(128) = LOWER(@packageVersion)

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Create dimension if not exists
			IF NOT EXISTS (SELECT Id FROM [Dimension_Package] (NOLOCK) WHERE LOWER([PackageId]) = @packageIdLowercase AND LOWER([PackageVersion]) = @packageVersionLowercase)
				INSERT INTO [Dimension_Package] ([PackageId], [PackageVersion])
					OUTPUT inserted.Id, inserted.PackageId, inserted.PackageVersion INTO @results
				VALUES (@packageIdLowercase, LOWER(@PackageVersion));
			ELSE
				INSERT INTO @results ([Id], [PackageId], [PackageVersion])
				SELECT	[Id], [PackageId], [PackageVersion]
				FROM	[dbo].[Dimension_Package] (NOLOCK)
				WHERE	LOWER([PackageId]) = @packageIdLowercase
						AND LOWER([PackageVersion]) = @packageVersionLowercase

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM package_Cursor
			INTO @packageId, @packageVersion
		END

		-- Close cursor
		CLOSE package_Cursor
		DEALLOCATE package_Cursor

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