using System;
using System.Data.Entity;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery.Jobs
{
    public class UpdateStatisticsJob : Job
    {
        private Func<DbContext> _contextThunk;

        public UpdateStatisticsJob(TimeSpan interval, Func<DbContext> contextThunk, TimeSpan timeout)
            : base("Update Package Download Statistics", interval, timeout)
        {
            if (contextThunk == null)
            {
                throw new ArgumentNullException("contextThunk");
            }
            _contextThunk = contextThunk;
        }

        public override Task Execute()
        {
            return new Task(UpdateStats);
        }

        private void UpdateStats()
        {
            const string sql = @"
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

UPDATE tmp
SET DownLoadCount = tmp.DownloadCount + p.DownloadCount
FROM @DownloadStats tmp INNER JOIN 
(
    SELECT [Key], DownloadCount
    FROM Packages
) p
ON p.[Key] = tmp.PackageKey

BEGIN TRANSACTION

    UPDATE p
    SET p.DownLoadCount = stats.DownloadCount
    FROM Packages p INNER JOIN @DownloadStats stats
    ON p.[Key] = stats.PackageKey

    UPDATE GallerySettings
    SET DownloadStatsLastAggregatedId = @mostRecentStatisticsId

COMMIT TRANSACTION

UPDATE pr
SET pr.DownLoadCount = totals.DownloadCount
FROM PackageRegistrations pr INNER JOIN
(
    SELECT PackageRegistrationKey, DownloadCount = SUM(DownloadCount)
    FROM Packages
    GROUP BY PackageRegistrationKey
) as totals
ON pr.[Key] = totals.PackageRegistrationKey";
            using (var context = _contextThunk())
            {
                context.Database.ExecuteSqlCommand(sql);
            }
        }
    }
}