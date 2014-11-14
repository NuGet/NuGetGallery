using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs.Common;

namespace Stats.AggregateInGallery
{
    internal class Job : JobBase
    {
        private const int MinBatchSize = 1000;
        private const int MaxBatchSize = 100000;
        private const int MaxFailures = 10;
        private static int CurrentFailures = 0;
        private static int CurrentMinBatchSize = MinBatchSize;
        private static int CurrentMaxBatchSize = MaxBatchSize;
        private static Dictionary<double, int> BatchTimes = new Dictionary<double, int>();

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder PackageDatabase { get; set; }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                PackageDatabase =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase, EnvironmentVariableKeys.SqlGallery));
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            Trace.TraceInformation("Aggregating statistics in {0}/{1}", PackageDatabase.DataSource, PackageDatabase.InitialCatalog);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            var count = await Aggregate();
            watch.Stop();

            if (count > 0)
            {
                double perSecond = count / watch.Elapsed.TotalSeconds;
                Trace.TraceInformation("===== Aggregated {0} records. Duration: {1}. Pace: {2}/second. =====", count, watch.Elapsed.ToString("G"), (int)perSecond);
            }
            else
            {
                Trace.TraceInformation("===== No statistics were aggregated. Duration: {0}. Finished. =====", watch.Elapsed.ToString("G"));
            }

            return true;
        }

        private async Task<int> Aggregate()
        {
            var watch = new Stopwatch();
            watch.Start();

            Trace.TraceInformation("Aggregating statitics in {0}/{1}", PackageDatabase.DataSource, PackageDatabase.InitialCatalog);
            CurrentMinBatchSize = MinBatchSize;
            CurrentMaxBatchSize = MaxBatchSize;

            bool needsAggregating = true;
            int totalAggregated = 0;

            var totalTime = new Stopwatch();
            totalTime.Start();

            while (needsAggregating)
            {
                double recordsPerSecond;
                int batchSize = GetNextBatchSize(out recordsPerSecond);
                int aggregated = 0;

                Trace.TraceInformation("Aggregating a batch of size: {0}. Optimistic Pace: {1}. -----", batchSize, (int)recordsPerSecond);

                try
                {
                    var batchWatch = new Stopwatch();
                    batchWatch.Start();
                    aggregated = await AggregateBatch(batchSize);
                    batchWatch.Stop();

                    totalAggregated += aggregated;

                    // If we had fewer records to aggregate than our batch size,
                    // then we've caught up and we can exit and let some more
                    // records accumulate.
                    if (aggregated < batchSize)
                    {
                        needsAggregating = false;
                    }
                    else
                    {
                        RecordSuccessfulBatchTime(batchSize, batchWatch.Elapsed);
                    }

                    CurrentFailures = 0;
                }
                catch (SqlException ex)
                {
                    Trace.TraceWarning("BATCH FAILED. Size: {0}. Attempt {1} of {2}. Error: {3}", batchSize, ++CurrentFailures, MaxFailures, ex.ToString());

                    // If we can't even process the min batch size, or we've maxed out on failures, then give up
                    if (batchSize <= CurrentMinBatchSize)
                    {
                        Trace.TraceError("Aborting - Unable to process minimum batch size of {0}.", batchSize);
                        break;
                    }
                    else if (CurrentFailures == MaxFailures)
                    {
                        Trace.TraceError("Aborting - Too many consecutive errors.");
                        break;
                    }

                    // Otherwise, let's reduce our batch size range
                    RecordFailedBatchSize(batchSize);
                }
            }

            watch.Stop();

            var perSecond = totalAggregated / watch.Elapsed.TotalSeconds;
            Trace.TraceInformation("Aggregated {0} records. Duration: {1}. Pace: {2}.", totalAggregated, watch.Elapsed.ToString("G"), (int)perSecond);

            return totalAggregated;
        }

        private async Task<int> AggregateBatch(int batchSize)
        {
            using (var connection = await PackageDatabase.ConnectTo())
            {
                var command = new SqlCommand(AggregateStatsSql, connection);
                command.Parameters.AddWithValue("@BatchSize", batchSize);

                return await command.ExecuteScalarAsync() as int? ?? 0;
            }
        }
        private const string AggregateStatsSql = @"
SET NOCOUNT ON

DECLARE @UpdatedGallerySettings TABLE
(
        MostRecentStatisticsId int
    ,   LastAggregatedStatisticsId int
)

DECLARE     @LatestGallerySettingPlusOffset INT
DECLARE     @Offset INT = @BatchSize

BEGIN TRANSACTION

    SET    @LatestGallerySettingPlusOffset = ISNULL(@Offset + (SELECT [DownloadStatsLastAggregatedId] FROM GallerySettings), @Offset)

    -- Get the next marker to move forward to
    UPDATE	GallerySettings
    SET		DownloadStatsLastAggregatedId = (
			    SELECT		MAX([Key])
			    FROM		(
						    SELECT		TOP (@BatchSize)
									    [Key]
						    FROM		PackageStatistics
						    WHERE		[Key] > (SELECT DownloadStatsLastAggregatedId FROM GallerySettings)
						    ORDER BY	[Key]
						    ) Batch
			) 
    OUTPUT	inserted.DownloadStatsLastAggregatedId AS MostRecentStatisticsId
		,	deleted.DownloadStatsLastAggregatedId AS LastAggregatedStatisticsId
    INTO    @UpdatedGallerySettings

	DECLARE @mostRecentStatisticsId int
    DECLARE @lastAggregatedStatisticsId int

    SELECT  TOP 1
            @mostRecentStatisticsId = MostRecentStatisticsId
        ,   @lastAggregatedStatisticsId = LastAggregatedStatisticsId
    FROM    @UpdatedGallerySettings

    SELECT  @lastAggregatedStatisticsId = ISNULL(@lastAggregatedStatisticsId, 0)

    IF (@mostRecentStatisticsId IS NULL) BEGIN
        ROLLBACK TRANSACTION
        RETURN
    END

    DECLARE @DownloadStats TABLE
    (
            PackageKey int PRIMARY KEY
        ,   DownloadCount int
    )

    DECLARE @AffectedPackages TABLE
    (
            PackageRegistrationKey int
    )

    -- Grab package statistics
    INSERT      @DownloadStats
    SELECT      stats.PackageKey, DownloadCount = COUNT(1)
    FROM        PackageStatistics stats
    WHERE       [Key] > @lastAggregatedStatisticsId
            AND [Key] <= @mostRecentStatisticsId
    GROUP BY    stats.PackageKey

    -- Aggregate Package-level stats
	UPDATE      Packages
    SET         Packages.DownloadCount = Packages.DownloadCount + stats.DownloadCount,
    			Packages.LastUpdated = GetUtcDate()
    OUTPUT      inserted.PackageRegistrationKey INTO @AffectedPackages
    FROM        Packages
    INNER JOIN  @DownloadStats stats ON Packages.[Key] = stats.PackageKey        
    
    -- Aggregate PackageRegistration stats
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

	UPDATE		GallerySettings
	SET			GallerySettings.TotalDownloadCount = GallerySettings.TotalDownloadCount + (SELECT ISNULL(SUM(DownloadCount), 0) FROM @DownloadStats)

    -- Return the number of stats aggregated
    SELECT      SUM(DownloadCount)
    FROM        @DownloadStats

COMMIT TRANSACTION
";

        private int GetNextBatchSize(out double recordsPerSecond)
        {
            // Every 100 runs, we will reset our time recordings and find a new best time all over
            if (BatchTimes.Count >= 100)
            {
                BatchTimes.Clear();

                CurrentMinBatchSize = MinBatchSize;
                CurrentMaxBatchSize = MaxBatchSize;
            }

            int nextBatchSize;

            if (BatchTimes.Count == 0)
            {
                nextBatchSize = CurrentMinBatchSize;
                recordsPerSecond = 100; // A baseline pace to expect
                Trace.WriteLine(string.Format("Sampling batch sizes. Min batch size: {0}; Max batch size: {1}.", CurrentMinBatchSize, CurrentMaxBatchSize));
            }
            else if (BatchTimes.Count < 11)
            {
                // We'll run through 11 iterations of our possible range, with 10% increments along the way.
                // Yes, 11. Because fenceposts.
                KeyValuePair<double, int> bestSoFar = BatchTimes.OrderByDescending(batch => batch.Key).First();
                nextBatchSize = CurrentMinBatchSize + ((CurrentMaxBatchSize - CurrentMinBatchSize) / 10 * BatchTimes.Count);
                recordsPerSecond = bestSoFar.Key; // Optimistically, we'll match the best time after it all levels out
                Trace.WriteLine(string.Format("Sampling batch sizes. Samples taken: {0}; Next sample size: {1}; Best sample size so far: {2} at {3} records per second.", BatchTimes.Count, nextBatchSize, bestSoFar.Value, (int)bestSoFar.Key));
            }
            else
            {
                IEnumerable<KeyValuePair<double, int>> bestBatches = BatchTimes.OrderByDescending(batch => batch.Key).Take(BatchTimes.Count / 4);
                string bestSizes = String.Join(", ", bestBatches.Select(batch => batch.Value));
                string bestPaces = String.Join(", ", bestBatches.Select(batch => (int)batch.Key));

                nextBatchSize = (int)bestBatches.Select(batch => batch.Value).Average();

                // Ensure the next batch size is within the allowable range
                nextBatchSize = Math.Max(Math.Min(nextBatchSize, CurrentMaxBatchSize), CurrentMinBatchSize);

                recordsPerSecond = bestBatches.Average(b => b.Key); // Optimistically, we'll match the average time of the best batches
                Trace.WriteLine(string.Format("Calculated the batch size of {0} using the best of {1} batches. Best batch sizes so far: {2}, running at the following paces (per second): {3}", nextBatchSize, BatchTimes.Count, bestSizes, bestPaces));
            }

            return nextBatchSize;
        }

        private void RecordSuccessfulBatchTime(int batchSize, TimeSpan elapsedTime)
        {
            double perSecond = batchSize / elapsedTime.TotalSeconds;
            BatchTimes[perSecond] = batchSize;

            Trace.WriteLine(string.Format("----- Successfully purged a batch. Size: {0}. Duration: {1}. Pace: {2}. -----", batchSize, elapsedTime.ToString("G"), (int)perSecond));
        }

        private void RecordFailedBatchSize(int batchSize)
        {
            if (BatchTimes.Any())
            {
                int maxSuccessfulMatch = BatchTimes.Values.Max();

                if (CurrentMaxBatchSize > maxSuccessfulMatch)
                {
                    // Split the difference between the max successful batch size and our batch size that just failed
                    CurrentMaxBatchSize = (maxSuccessfulMatch + batchSize) / 2;
                }
                else
                {
                    CurrentMaxBatchSize = CurrentMaxBatchSize * 2 / 3;
                }

                // Ensure the Max doesn't fall below the Min
                CurrentMaxBatchSize = Math.Max(CurrentMaxBatchSize, CurrentMinBatchSize);
                Trace.WriteLine(string.Format("Capping the max batch size to the average of the largest successful batch size of {0} and the last attempted batch size of {1}. New max batch size is {2}.", maxSuccessfulMatch, batchSize, CurrentMaxBatchSize));
            }
            else
            {
                CurrentMinBatchSize = CurrentMinBatchSize / 2;
                CurrentMaxBatchSize = CurrentMaxBatchSize * 2 / 3;

                // Ensure the Max doesn't fall below the Min
                CurrentMaxBatchSize = Math.Max(CurrentMaxBatchSize, CurrentMinBatchSize);
                Trace.WriteLine(string.Format("Reducing the batch size window down to {0} - {1}", CurrentMinBatchSize, CurrentMaxBatchSize));
            }
        }
    }
}
