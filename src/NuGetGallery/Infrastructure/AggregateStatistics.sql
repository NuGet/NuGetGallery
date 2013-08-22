SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AggregateStatistics]') AND type in (N'P', N'PC'))
BEGIN
	Drop procedure [dbo].[AggregateStatistics]
END
GO
EXEC dbo.sp_executesql @statement = N'
CREATE PROCEDURE [dbo].[AggregateStatistics]
AS
    SET NOCOUNT ON

    DECLARE @mostRecentStatisticsId int
    DECLARE @lastAggregatedStatisticsId int

    SELECT  @mostRecentStatisticsId = MAX([Key]) FROM PackageStatistics
    SELECT  @lastAggregatedStatisticsId = DownloadStatsLastAggregatedId FROM GallerySettings
    SELECT  @lastAggregatedStatisticsId = ISNULL(@lastAggregatedStatisticsId, 0)

    IF (@mostRecentStatisticsId IS NULL)
        RETURN

    DECLARE @DownloadStats TABLE
    (
            PackageKey int PRIMARY KEY
        ,   DownloadCount int
    )

    DECLARE @AffectedPackages TABLE
    (
            PackageRegistrationKey int
    )

    INSERT      @DownloadStats
    SELECT      stats.PackageKey, DownloadCount = COUNT(1)
    FROM        PackageStatistics stats
    WHERE       [Key] > @lastAggregatedStatisticsId
            AND [Key] <= @mostRecentStatisticsId
    GROUP BY    stats.PackageKey

    BEGIN TRANSACTION

        UPDATE      Packages
        SET         Packages.DownloadCount = Packages.DownloadCount + stats.DownloadCount,
					Packages.LastUpdated = GetUtcDate()
        OUTPUT      inserted.PackageRegistrationKey INTO @AffectedPackages
        FROM        Packages
        INNER JOIN  @DownloadStats stats ON Packages.[Key] = stats.PackageKey        
        
        UPDATE      PackageRegistrations
        SET         DownloadCount = TotalDownloadCount
        FROM        (
                    SELECT      Packages.PackageRegistrationKey
                            ,   SUM(Packages.DownloadCount) AS TotalDownloadCount
                    FROM        (SELECT DISTINCT PackageRegistrationKey FROM @AffectedPackages) affected
                    INNER JOIN  Packages ON Packages.PackageRegistrationKey = affected.PackageRegistrationKey
                    GROUP BY    Packages.PackageRegistrationKey
                    ) AffectedPackageRegistrations
        INNER JOIN  PackageRegistrations ON PackageRegistrations.[Key] = AffectedPackageRegistrations.PackageRegistrationKey
                
        UPDATE      GallerySettings
        SET         DownloadStatsLastAggregatedId = @mostRecentStatisticsId
				,	TotalDownloadCount = (SELECT SUM(DownloadCount) FROM PackageRegistrations)

    COMMIT TRANSACTION
' 
GO