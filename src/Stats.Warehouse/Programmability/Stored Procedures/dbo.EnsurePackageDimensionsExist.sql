CREATE PROCEDURE [dbo].[EnsurePackageDimensionsExist]
	@packages [dbo].[PackageDimensionTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;
	
	DECLARE @results TABLE
	(
		[Id]				INT				NOT NULL PRIMARY KEY,
		[PackageId]         NVARCHAR(255)	NOT NULL,
		[PackageVersion]	NVARCHAR(128)	NOT NULL,
		UNIQUE NONCLUSTERED ([PackageId], [PackageVersion])
	)

	-- Select existing packages and insert them into the results table
	INSERT INTO @results ([Id], [PackageId], [PackageVersion])
		SELECT DISTINCT	P.[Id], P.[PackageId], P.[PackageVersion]
		FROM	[dbo].[Dimension_Package] AS P (NOLOCK)
		INNER JOIN	@packages AS I
		ON	P.[LowercasedPackageId] = LOWER(I.PackageId)
			AND P.[LowercasedPackageVersion] = LOWER(I.PackageVersion)

	-- Insert new packages
	BEGIN TRY
		SET TRANSACTION ISOLATION LEVEL READ COMMITTED
		BEGIN TRANSACTION

			INSERT INTO [Dimension_Package] ([PackageId], [PackageVersion])
				OUTPUT inserted.Id, inserted.PackageId, inserted.PackageVersion INTO @results
			SELECT	[PackageId], [PackageVersion]
				FROM	@packages
			EXCEPT
			SELECT	[PackageId], [PackageVersion]
				FROM	@results

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