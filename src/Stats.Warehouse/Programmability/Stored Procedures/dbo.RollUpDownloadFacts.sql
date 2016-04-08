CREATE PROCEDURE [dbo].[RollUpDownloadFacts]
	@MinAgeInDays INT = 90
AS
BEGIN
	SET NOCOUNT ON;

	IF @MinAgeInDays IS NULL
	BEGIN
		SELECT	0 AS 'DeletedDownloadFacts',
				0 AS 'DeletedProjectTypeLinks',
				0 AS 'InsertedDownloadFacts',
				0 AS 'TotalDownloadCount',
				'Package Dimension Id cannot be null' AS 'ErrorMessage'

		RETURN
	END
	ELSE
	BEGIN

		DECLARE @ExecutionResultsTable TABLE
		(
			[DeletedDownloadFacts] INT,
			[DeletedProjectTypeLinks] INT,
			[InsertedDownloadFacts] INT,
			[TotalDownloadCount] INT,
			[ErrorMessage] NVARCHAR(MAX)
		)

		DECLARE @DeletedProjectTypeRecords INT = 0
		DECLARE @DeletedDownloadFactRecords INT = 0
		DECLARE @InsertedRecords INT = 0
		DECLARE @ErrorMessage NVARCHAR(MAX) = ''
		DECLARE @Dimension_Package_Id INT

		DECLARE @DatesTable TABLE
		(
			[Id] INT NOT NULL PRIMARY KEY
		)

		INSERT INTO @DatesTable
		SELECT	[Id]
		FROM	[dbo].[Dimension_Date] (NOLOCK)
		WHERE	[Date] IS NOT NULL
			AND	[Date] < DATEADD(DAY, -@MinAgeInDays, GETUTCDATE())
			AND [Id] >= (
							SELECT	MIN([Dimension_Date_Id])
							FROM	[dbo].[Fact_Download] (NOLOCK)
							WHERE	[Dimension_Date_Id] <> -1
						)

		DECLARE @CursorPosition INT = 0
		DECLARE @TotalCursorPositions INT = 0

		SELECT	@TotalCursorPositions = COUNT(DISTINCT p.[Id])
			FROM	[dbo].[Dimension_Package] AS p (NOLOCK)
			INNER JOIN	[dbo].[Fact_Download] AS f (NOLOCK)
			ON		f.[Dimension_Package_Id] = p.[Id]
			WHERE	(f.[Dimension_Date_Id] IN (SELECT [Id] FROM @DatesTable));

		DECLARE PackageCursor CURSOR FOR
			SELECT	DISTINCT p.[Id]
			FROM	[dbo].[Dimension_Package] AS p (NOLOCK)
			INNER JOIN	[dbo].[Fact_Download] AS f (NOLOCK)
			ON		f.[Dimension_Package_Id] = p.[Id]
			WHERE	(f.[Dimension_Date_Id] IN (SELECT [Id] FROM @DatesTable))
			ORDER BY	p.[Id];

		OPEN PackageCursor

		FETCH NEXT FROM PackageCursor
		INTO @Dimension_Package_Id

		WHILE @@FETCH_STATUS = 0 AND @ErrorMessage = ''
		BEGIN

			DECLARE @DownloadCount INT = 0

			SELECT	@DownloadCount = SUM(f.[DownloadCount])
			FROM	[dbo].[Fact_Download] AS f (NOLOCK)
			WHERE	(f.[Dimension_Date_Id] = -1 OR f.[Dimension_Date_Id] IN (SELECT [Id] FROM @DatesTable))
				AND f.[Dimension_Package_Id] = @Dimension_Package_Id

			BEGIN TRANSACTION

			BEGIN TRY

				DELETE
				FROM	[dbo].[Fact_Download_Dimension_ProjectType]
				WHERE	[Fact_Download_Id] IN	(
														SELECT	[Id]
														FROM	[dbo].[Fact_Download] (NOLOCK)
														WHERE	[Dimension_Package_Id] = @Dimension_Package_Id
															AND	[Dimension_Date_Id] IN	(SELECT [Id] FROM @DatesTable)
												)
				SET @DeletedProjectTypeRecords = @DeletedProjectTypeRecords + @@rowcount

				DELETE
				FROM	[dbo].[Fact_Download]
				WHERE	[Dimension_Package_Id] = @Dimension_Package_Id
					AND	([Dimension_Date_Id] = -1 OR [Dimension_Date_Id] IN (SELECT [Id] FROM @DatesTable))

				SET @DeletedDownloadFactRecords = @DeletedDownloadFactRecords + @@rowcount

				INSERT INTO [dbo].[Fact_Download]
				(
					[Dimension_Package_Id],
					[Dimension_Date_Id],
					[Dimension_Time_Id],
					[Dimension_Operation_Id],
					[Dimension_Client_Id],
					[Dimension_Platform_Id],
					[Fact_UserAgent_Id],
					[Fact_LogFileName_Id],
					[Fact_EdgeServer_IpAddress_Id],
					[DownloadCount],
					[Timestamp]
				)
				VALUES
				(
					@Dimension_Package_Id,
					-1,
					0,
					1,
					1,
					1,
					1,
					1,
					1,
					@DownloadCount,
					GETUTCDATE()
				)

				SET @InsertedRecords = @InsertedRecords + @@rowcount

				COMMIT TRANSACTION
			END TRY
			BEGIN CATCH

				SET @ErrorMessage = ERROR_MESSAGE()

				IF @@TRANCOUNT > 0
					ROLLBACK TRANSACTION

			END CATCH

			FETCH NEXT FROM PackageCursor
			INTO @Dimension_Package_Id
		END

		CLOSE PackageCursor;
		DEALLOCATE PackageCursor;


		INSERT INTO @ExecutionResultsTable
		SELECT	@DeletedDownloadFactRecords	'DeletedDownloadFacts',
				@DeletedProjectTypeRecords	'DeletedProjectTypeLinks',
				@InsertedRecords			'InsertedDownloadFacts',
				@DownloadCount				'TotalDownloadCount',
				@ErrorMessage				'ErrorMessage'

		SELECT	[DeletedDownloadFacts],
				[DeletedProjectTypeLinks],
				[InsertedDownloadFacts],
				[TotalDownloadCount],
				[ErrorMessage]
		FROM @ExecutionResultsTable

	END
END