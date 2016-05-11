CREATE PROCEDURE [dbo].[StoreLogFileAggregates]
	@packageDownloadsByDate [dbo].[LogFileAggregatesPackageDownloadsByDateTableType] READONLY
AS
BEGIN
	SET NOCOUNT ON;

	BEGIN TRY
		DECLARE @LogFileName NVARCHAR(255)
		DECLARE @Dimension_Date_Id INT
		DECLARE @PackageDownloads INT

		-- Open Cursor
		DECLARE aggregates_Cursor CURSOR FOR
			SELECT	[LogFileName], [Dimension_Date_Id], [PackageDownloads]
			FROM	@packageDownloadsByDate

		OPEN	aggregates_Cursor FETCH NEXT
		FROM	aggregates_Cursor
		INTO	@LogFileName, @Dimension_Date_Id, @PackageDownloads

		WHILE @@FETCH_STATUS = 0
		BEGIN
			SET TRANSACTION ISOLATION LEVEL READ COMMITTED
			BEGIN TRANSACTION

			IF NOT EXISTS (SELECT [Id] FROM [dbo].[Agg_PackageDownloads_LogFile] (NOLOCK) WHERE [LogFileName] = @LogFileName AND [Dimension_Date_Id] = @Dimension_Date_Id)
					INSERT INTO [dbo].[Agg_PackageDownloads_LogFile] ([LogFileName], [Dimension_Date_Id], [PackageDownloads])
					VALUES (@LogFileName, @Dimension_Date_Id, @PackageDownloads);
			ELSE
			BEGIN
				UPDATE	[dbo].[Agg_PackageDownloads_LogFile]
				SET		[PackageDownloads] = @PackageDownloads
				WHERE	[LogFileName] = @LogFileName
						AND [Dimension_Date_Id] = @Dimension_Date_Id
			END

			COMMIT

			-- Advance cursor
			FETCH NEXT FROM aggregates_Cursor
			INTO @LogFileName, @Dimension_Date_Id, @PackageDownloads

		END

		-- Close cursor
		CLOSE aggregates_Cursor
		DEALLOCATE aggregates_Cursor

	END TRY
	BEGIN CATCH

		IF @@TRANCOUNT > 0
			ROLLBACK;

		THROW

	END CATCH

END