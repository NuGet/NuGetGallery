CREATE PROCEDURE [dbo].[GetPackageTranslations]
AS
BEGIN
	SET NOCOUNT ON;

	-- Find the manually maintained support mappings for incorrectly parsed package dimensions
	DECLARE @translations AS TABLE
	(
		[CorrectedPackageId] INT NOT NULL,
		[IncorrectPackageId] NVARCHAR(128) NOT NULL,
		[IncorrectPackageVersion] NVARCHAR(128) NOT NULL
	)

	INSERT INTO @translations
	SELECT	[CorrectedPackageId],
			[IncorrectPackageId],
			[IncorrectPackageVersion]
	FROM	[dbo].[Support_TranslatePackages] (NOLOCK)
	ORDER BY	[IncorrectPackageId] ASC,
				[IncorrectPackageVersion] DESC

	-- Find the incorrectly parsed package id and versions that got inserted into the package dimension table
	DECLARE @packageIdCorrections AS TABLE
	(
		[IncorrectPackageId] INT NOT NULL,
		[CorrectedPackageId] INT NOT NULL
	)

	INSERT INTO @packageIdCorrections
	SELECT	P.[Id] AS [IncorrectPackageId]
			,T.[CorrectedPackageId]
	FROM	[dbo].[Dimension_Package] AS P (NOLOCK)
	INNER JOIN	@translations AS T
	ON		P.PackageId = T.IncorrectPackageId
			AND P.PackageVersion = T.IncorrectPackageVersion

	-- Correct the fact table by adjusting the package dimension mapping
	BEGIN TRY

		DECLARE @incorrectPackageId INT
		DECLARE @correctedPackageId INT

		-- Open Cursor
		DECLARE mapping_Cursor CURSOR FOR
			SELECT	[IncorrectPackageId], [CorrectedPackageId]
			FROM	@packageIdCorrections

		OPEN	mapping_Cursor FETCH NEXT
		FROM	mapping_Cursor
		INTO	@incorrectPackageId, @correctedPackageId

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
			BEGIN TRANSACTION

			-- Update facts with incorrect package dimension
			UPDATE	[dbo].[Fact_Download]
			SET		[Dimension_Package_Id] = @correctedPackageId
			WHERE	[Dimension_Package_Id] = @incorrectPackageId

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM mapping_Cursor
			INTO @incorrectPackageId, @correctedPackageId
		END

		-- Close cursor
		CLOSE mapping_Cursor
		DEALLOCATE mapping_Cursor

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH


	-- return translations
	SELECT	[CorrectedPackageId],
			[IncorrectPackageId],
			[IncorrectPackageVersion]
	FROM	@translations

END
