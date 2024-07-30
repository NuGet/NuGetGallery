CREATE PROCEDURE [dbo].[RollUpDownloadFacts]
	-- 6-weeks of data retention + 1 day (cursor)
	@MinAgeInDays INT = 43
AS
BEGIN
	SET NOCOUNT ON;

	-- We want this query to always be the deadlock victim.
	-- Rolling up existing download facts is not as important as inserting new statistics.
	SET DEADLOCK_PRIORITY LOW;

	IF @MinAgeInDays IS NOT NULL
	BEGIN

		DECLARE @MaxDimensionDateId INT = -1
		DECLARE @Dimension_Package_Id INT
		DECLARE @DownloadCount INT = 0
		DECLARE @RecordCountToRemove INT = 0
		DECLARE @CursorPosition INT = 0
		DECLARE @TotalCursorPositions INT = 0
		DECLARE @Msg NVARCHAR(MAX) = ''

		DECLARE @PackageIdTable TABLE
		(
			[Id] INT NOT NULL PRIMARY KEY,
			[RecordCountToRemove] INT NOT NULL
		)

		SELECT	@MaxDimensionDateId = MAX([Id])
		FROM	[dbo].[Dimension_Date] (NOLOCK)
		WHERE	[Date] IS NOT NULL
			AND	[Date] < DATEADD(DAY, -@MinAgeInDays, GETUTCDATE())

		SET @Msg = 'Fetched ' + CAST(@@rowcount AS VARCHAR) + ' dates.';
		RAISERROR(@Msg, 0, 1) WITH NOWAIT;

		INSERT INTO @PackageIdTable
		SELECT	DISTINCT p.[Id],
				COUNT(f.[Id]) 'RecordCountToRemove'
		FROM	[dbo].[Dimension_Package] AS p (NOLOCK)
		INNER JOIN	[dbo].[Fact_Download] AS f (NOLOCK)
		ON		f.[Dimension_Package_Id] = p.[Id]
		WHERE	f.[Dimension_Date_Id] <> -1
			AND f.[Dimension_Date_Id] <= @MaxDimensionDateId
		GROUP BY	p.[Id]
		HAVING  COUNT(f.[Id]) > 1
		ORDER BY	RecordCountToRemove DESC;

		SELECT	@TotalCursorPositions = COUNT(DISTINCT [Id])
		FROM	@PackageIdTable;

		SET @Msg = 'Fetched ' + CAST(@TotalCursorPositions AS VARCHAR) + ' package dimension IDs.';
		RAISERROR(@Msg, 0, 1) WITH NOWAIT;

		DECLARE PackageCursor CURSOR FOR
			SELECT	[Id],
					[RecordCountToRemove]
			FROM	@PackageIdTable
			ORDER BY	[RecordCountToRemove] DESC;

		OPEN PackageCursor

		FETCH NEXT FROM PackageCursor
		INTO @Dimension_Package_Id, @RecordCountToRemove

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET @CursorPosition = @CursorPosition + 1

			DECLARE @ProgressPct FLOAT = ROUND((@CursorPosition / (@TotalCursorPositions * 1.0))* 100, 2)

			SET @Msg = 'Cursor: ' + CAST(@CursorPosition AS VARCHAR) + '/' + CAST(@TotalCursorPositions AS VARCHAR) + ' [' + CAST(@ProgressPct AS VARCHAR) + ' pct.]';
			RAISERROR(@Msg, 0, 1) WITH NOWAIT;

			SELECT	@DownloadCount = SUM(f.[DownloadCount])
			FROM	[dbo].[Fact_Download] AS f (NOLOCK)
			WHERE	f.[Dimension_Date_Id] <= @MaxDimensionDateId
				AND f.[Dimension_Package_Id] = @Dimension_Package_Id
			GROUP BY	f.[Dimension_Package_Id]

			SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': ' + CAST(@DownloadCount AS VARCHAR) + ' downloads, ' + CAST(@RecordCountToRemove AS VARCHAR) + ' records to be removed';
			RAISERROR(@Msg, 0, 1) WITH NOWAIT

			BEGIN TRANSACTION

			BEGIN TRY

				DECLARE @DeletedRecords INT = 0
				DECLARE @InsertedRecords INT = 0

				SET @DeletedRecords = 0

				DELETE
				FROM	[dbo].[Fact_Download]
				WHERE	[Dimension_Package_Id] = @Dimension_Package_Id
					AND	[Dimension_Date_Id] <= @MaxDimensionDateId

				SET @DeletedRecords = @@rowcount

				SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Deleted ' + CAST(@DeletedRecords AS VARCHAR) + ' records from [dbo].[Fact_Download]';
				RAISERROR(@Msg, 0, 1) WITH NOWAIT

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

				SET @InsertedRecords = @@rowcount
				SET @Msg = 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Inserted ' + CAST(@InsertedRecords AS VARCHAR) + ' record for ' + CAST(@DownloadCount AS VARCHAR) + ' downloads';
				RAISERROR(@Msg, 0, 1) WITH NOWAIT

				COMMIT TRANSACTION
			END TRY
			BEGIN CATCH
				ROLLBACK TRANSACTION

				PRINT 'Package Dimension ID ' + CAST(@Dimension_Package_Id AS VARCHAR) + ': Rolled back transaction - ' + ERROR_MESSAGE();

			END CATCH

			FETCH NEXT FROM PackageCursor
			INTO @Dimension_Package_Id, @RecordCountToRemove
		END

		CLOSE PackageCursor;
		DEALLOCATE PackageCursor;

		PRINT 'FINISHED!';
	END
END