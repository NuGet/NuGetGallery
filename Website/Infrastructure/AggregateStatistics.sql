SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AggregateStatistics]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'
CREATE PROCEDURE [dbo].[AggregateStatistics]
AS 

	DECLARE @mostRecentStatisticsId int
	DECLARE @lastAggregatedStatisticsId int

	SELECT @mostRecentStatisticsId = MAX([Key]) FROM PackageStatistics
	SELECT @lastAggregatedStatisticsId = DownloadStatsLastAggregatedId FROM GallerySettings
	SELECT @lastAggregatedStatisticsId = ISNULL(@lastAggregatedStatisticsId, 0)

	IF (@mostRecentStatisticsId IS NULL)
	RETURN

	DECLARE @DownloadStats TABLE
	(
	  PackageKey int PRIMARY KEY,
	  DownloadCount int
	)

	INSERT INTO @DownloadStats
	SELECT stats.PackageKey, DownloadCount = COUNT(1)
	FROM PackageStatistics stats
	WHERE [Key] > @lastAggregatedStatisticsId AND 
		[Key] <= @mostRecentStatisticsId
	GROUP BY stats.PackageKey

	BEGIN TRANSACTION

		UPDATE p
		SET p.DownloadCount = p.DownloadCount + stats.DownloadCount
		FROM Packages p INNER JOIN @DownloadStats stats
		ON p.[Key] = stats.PackageKey
    
		IF @@ROWCOUNT > 0
		BEGIN
			UPDATE pr
			SET pr.DownloadCount = totals.DownloadCount
			OUTPUT inserted.[Id], deleted.DownloadCount AS OldDownloadCount, inserted.DownloadCount AS NewDownloadCount
			FROM PackageRegistrations pr INNER JOIN
			(
				SELECT		PackageRegistrationKey, DownloadCount = SUM(Packages.DownloadCount)
				FROM		PackageRegistrations
				INNER JOIN	Packages ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
				WHERE		EXISTS(
								SELECT		*
								FROM		Packages
								WHERE		Packages.PackageRegistrationKey = PackageRegistrations.[Key]
										AND	EXISTS(SELECT * FROM @DownloadStats stats WHERE stats.PackageKey = Packages.[Key]))
				GROUP BY PackageRegistrationKey
			) as totals
			ON pr.[Key] = totals.PackageRegistrationKey
		END    

		UPDATE GallerySettings
		SET DownloadStatsLastAggregatedId = @mostRecentStatisticsId

	COMMIT TRANSACTION
' 
END
GO