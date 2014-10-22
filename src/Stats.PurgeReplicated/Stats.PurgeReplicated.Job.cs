using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs.Common;

namespace Stats.PurgeReplicated
{
    internal class Job : JobBase
    {
        private static readonly JobEventSource JobEventSourceLog = JobEventSource.Log;
        private const int MinBatchSize = 1000;
        private const int MaxBatchSize = 50000;
        private const int MaxFailures = 10;
        private static int CurrentFailures = 0;
        private static int CurrentMinBatchSize = MinBatchSize;
        private static int CurrentMaxBatchSize = MaxBatchSize;
        private static Dictionary<double, int> BatchTimes = new Dictionary<double, int>();
        private static TimeSpan MaxBatchTime = TimeSpan.FromSeconds(
            30 + // Get the LastOriginalKey from the warehouse
            30 + // Get the batch from the source
            30 + // Put the batch into the destination
            30); // Some buffer time

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder Source { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing warehouse data.
        /// </summary>
        public SqlConnectionStringBuilder Destination { get; set; }

        /// <summary>
        /// The number of days' statistics to keep after replication.
        /// </summary>
        public int DaysToKeep { get; set; }

        public Job() : base(JobEventSource.Log) { }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                // Init member variables
                Source =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.SourceDatabase,
                            EnvironmentVariableKeys.SqlGallery));
                Destination =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.DestinationDatabase,
                            EnvironmentVariableKeys.SqlWarehouse));

                DaysToKeep = JobConfigManager.TryGetIntArgument(jobArgsDictionary, "DaysToKeep") ?? 7;

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
            JobEventSourceLog.PurgingReplicatedStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            var count = await Purge();
            watch.Stop();

            if (count > 0)
            {
                double perSecond = count / watch.Elapsed.TotalSeconds;
                JobEventSourceLog.PurgedReplicatedStatistics(count, watch.Elapsed.TotalSeconds, (int)perSecond);
            }
            else
            {
                JobEventSourceLog.NoStatisticsToPurge(watch.Elapsed.TotalSeconds);
            }

            return true;
        }

        private async Task<int> Purge()
        {
            JobEventSourceLog.GettingReplicationCursor(Destination.DataSource, Destination.InitialCatalog);
            DateTime? minTimestampToKeep = await GetMinTimestampToKeep(Destination);
            JobEventSourceLog.GotReplicationCursor(minTimestampToKeep.HasValue ? minTimestampToKeep.ToString() : "<null>");

            if (minTimestampToKeep == null)
            {
                return 0;
            }

            JobEventSourceLog.PurgingStatistics(Source.DataSource, Source.InitialCatalog);
            var watch = new Stopwatch();
            watch.Start();
            int purged = await PurgeCore(minTimestampToKeep.Value);
            watch.Stop();

            var perSecond = purged / watch.Elapsed.TotalSeconds;
            JobEventSourceLog.PurgedStatistics(purged, watch.Elapsed.TotalSeconds, (int)perSecond);

            return purged;
        }

        private async Task<DateTime?> GetMinTimestampToKeep(SqlConnectionStringBuilder Destination)
        {
            // Get the most recent cursor window that is older than the days we want to keep.
            // By getting the MAX(MinTimestamp), we'll delete statistics older than the beginning of the
            // most recent window that has begun processing (but isn't guaranteed to have completed).

            // Note that we made sure to treat DaysToKeep as a NEGATIVE number for the expected behavior
            var sql = @"
                SELECT      MAX(MinTimestamp)
                FROM        CollectorCursor
                WHERE       MinTimestamp <= DATEADD(day, -ABS(@DaysToKeep), convert(date, GETUTCDATE()))";

            using (var connection = await Destination.ConnectTo())
            {
                SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@DaysToKeep", DaysToKeep);

                return await command.ExecuteScalarAsync() as DateTime?;
            }
        }

        private async Task<int> PurgeCore(DateTime minTimestampToKeep)
        {
            CurrentMinBatchSize = MinBatchSize;
            CurrentMaxBatchSize = MaxBatchSize;

            bool needsPurging = true;
            int totalPurged = 0;
            int failures = 0;

            var totalTime = new Stopwatch();
            totalTime.Start();

            while (needsPurging)
            {
                double recordsPerSecond;
                int batchSize = GetNextBatchSize(out recordsPerSecond);
                int purged = 0;

                JobEventSourceLog.PurgingBatch(batchSize);

                try
                {
                    var watch = new Stopwatch();
                    watch.Start();
                    purged = await PurgeBatch(minTimestampToKeep, batchSize);
                    watch.Stop();

                    if (purged == 0)
                    {
                        needsPurging = false;
                    }
                    else
                    {
                        RecordSuccessfulBatchTime(batchSize, watch.Elapsed);
                        totalPurged += purged;
                    }

                    CurrentFailures = 0;
                }
                catch (Exception ex)
                {
                    JobEventSourceLog.BatchFailed(batchSize, ++CurrentFailures, MaxFailures, ex.ToString());

                    // If we can't even process the min batch size, or we've maxed out on failures, then give up
                    if (batchSize <= CurrentMinBatchSize)
                    {
                        JobEventSourceLog.UnableToProcessMinimumBatchSize(batchSize);
                        break;
                    }
                    else if (CurrentFailures == MaxFailures)
                    {
                        JobEventSourceLog.AbortingTooManyErrors();
                        break;
                    }

                    // Otherwise, let's reduce our batch size range
                    RecordFailedBatchSize(batchSize);
                }

                recordsPerSecond = totalPurged / totalTime.Elapsed.TotalSeconds;
            }

            return totalPurged;
        }

        private async Task<int> PurgeBatch(DateTime minTimestampToKeep, int batchSize)
        {
            var sql = @"
                DELETE      TOP(@BatchSize) [PackageStatistics]
                WHERE       [Timestamp] < @MinTimestampToKeep
                        AND [Key] < (SELECT [DownloadStatsLastAggregatedId] FROM [GallerySettings])

                SELECT      @@ROWCOUNT";

            using (var connection = await Source.ConnectTo())
            {
                var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@BatchSize", batchSize);
                command.Parameters.AddWithValue("@MinTimestampToKeep", minTimestampToKeep);

                return await command.ExecuteScalarAsync() as int? ?? 0;
            }
        }

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
                JobEventSourceLog.UsingFirstSampleBatchSize(CurrentMinBatchSize, CurrentMaxBatchSize);
            }
            else if (BatchTimes.Count < 11)
            {
                // We'll run through 11 iterations of our possible range, with 10% increments along the way.
                // Yes, 11. Because fenceposts.
                KeyValuePair<double, int> bestSoFar = BatchTimes.OrderByDescending(batch => batch.Key).First();
                nextBatchSize = CurrentMinBatchSize + ((CurrentMaxBatchSize - CurrentMinBatchSize) / 10 * BatchTimes.Count);
                recordsPerSecond = bestSoFar.Key; // Optimistically, we'll match the best time after it all levels out
                JobEventSourceLog.UsingNextSampleBatchSize(BatchTimes.Count, nextBatchSize, bestSoFar.Value, (int)bestSoFar.Key);
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
                JobEventSourceLog.UsingCalculatedBatchSize(nextBatchSize, BatchTimes.Count, bestSizes, bestPaces);
            }

            return nextBatchSize;
        }

        private void RecordSuccessfulBatchTime(int batchSize, TimeSpan elapsedTime)
        {
            double perSecond = batchSize / elapsedTime.TotalSeconds;
            BatchTimes[perSecond] = batchSize;

            JobEventSourceLog.SuccessfulBatch(batchSize, elapsedTime.TotalSeconds, (int)perSecond);
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
                JobEventSourceLog.CappingMaxBatchSize(maxSuccessfulMatch, batchSize, CurrentMaxBatchSize);
            }
            else
            {
                CurrentMinBatchSize = CurrentMinBatchSize / 2;
                CurrentMaxBatchSize = CurrentMaxBatchSize * 2 / 3;

                // Ensure the Max doesn't fall below the Min
                CurrentMaxBatchSize = Math.Max(CurrentMaxBatchSize, CurrentMinBatchSize);
                JobEventSourceLog.ReducingBatchSizes(CurrentMinBatchSize, CurrentMaxBatchSize);
            }
        }
    }

    [EventSource(Name = "Outercurve-NuGet-Jobs-PurgeReplicatedStatistics")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();
        private JobEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Purging statistics that have been replicated from {0}/{1} to {2}/{3}.")]
        public void PurgingReplicatedStatistics(string sourceServer, string sourceDatabase, string destinationServer, string destinationDatabase)
        { WriteEvent(1, sourceServer, sourceDatabase, destinationServer, destinationDatabase); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Message = "===== Purged {0} records. Duration: {1}. Pace: {2}/second. =====")]
        public void PurgedReplicatedStatistics(int count, double elapsedSeconds, int perSecond)
        { WriteEvent(2, count, TimeSpan.FromSeconds(elapsedSeconds).ToString(), perSecond); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "===== There are no records to purge. Duration: {0}. Finished. =====")]
        public void NoStatisticsToPurge(double elapsedSeconds)
        { WriteEvent(3, TimeSpan.FromSeconds(elapsedSeconds).ToString()); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Getting the replication cursor from {0}/{1}.")]
        public void GettingReplicationCursor(string destinationServer, string destinationDatabase)
        { WriteEvent(4, destinationServer, destinationDatabase); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Message = "Got the replication cursor. Value: {0}.")]
        public void GotReplicationCursor(string minTimestampToKeep)
        { WriteEvent(5, minTimestampToKeep); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Message = "Purging records from {0}/{1}...")]
        public void PurgingStatistics(string sourceServer, string sourceDatabase)
        { WriteEvent(6, sourceServer, sourceDatabase); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Message = "Purged {0} records. Duration: {1}. Pace: {2}.")]
        public void PurgedStatistics(int purged, double elapsedSeconds, int perSecond)
        { WriteEvent(7, purged, TimeSpan.FromSeconds(elapsedSeconds).ToString(), perSecond); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Message = "----- Purging a batch. Size: {0}. -----")]
        public void PurgingBatch(int batchSize)
        { WriteEvent(8, batchSize); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Message = "----- Successfully purged a batch. Size: {0}. Duration: {1}. Pace: {2}. -----")]
        public void SuccessfulBatch(int batchSize, double elapsedSeconds, int perSecond)
        { WriteEvent(9, batchSize, TimeSpan.FromSeconds(elapsedSeconds).ToString(), perSecond); }

        [Event(
            eventId: 10, Level = EventLevel.Warning,
            Message = "BATCH FAILED. Size: {0}. Attempt {1} of {2}. Error: {3}")]
        public void BatchFailed(int batchSize, int attempt, int maxAttempts, string error)
        { WriteEvent(10, batchSize, attempt, maxAttempts, error); }

        [Event(
            eventId: 11, Level = EventLevel.Error,
            Message = "Aborting - Unable to process minimum batch size of {0}.")]
        public void UnableToProcessMinimumBatchSize(int batchSize)
        { WriteEvent(11, batchSize); }

        [Event(
            eventId: 12, Level = EventLevel.Informational,
            Message = "Sampling batch sizes. Min batch size: {0}; Max batch size: {1}.")]
        public void UsingFirstSampleBatchSize(int minBatch, int maxBatch)
        { WriteEvent(12, minBatch, maxBatch); }

        [Event(
            eventId: 13, Level = EventLevel.Informational,
            Message = "Sampling batch sizes. Samples taken: {0}; Next sample size: {1}; Best sample size so far: {2} at {3} records per second.")]
        public void UsingNextSampleBatchSize(int samplesTaken, int sampleSize, int bestSizeSoFar, int recordsPerSecond)
        { WriteEvent(13, samplesTaken, sampleSize, bestSizeSoFar, recordsPerSecond); }

        [Event(
            eventId: 14, Level = EventLevel.Informational,
            Message = "Calculated the batch size of {0} using the best of {1} batches. Best batch sizes so far: {2}, running at the following paces (per second): {3}")]
        public void UsingCalculatedBatchSize(int sampleSize, int timesRecorded, string bestBatchSizes, string bestBatchSizePaces)
        { WriteEvent(14, sampleSize, timesRecorded, bestBatchSizes, bestBatchSizePaces); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Message = "Capping the max batch size to the average of the largest successful batch size of {0} and the last attempted batch size of {1}. New max batch size is {2}.")]
        public void CappingMaxBatchSize(int largestSucessful, int lastAttempt, int maxBatchSize)
        { WriteEvent(15, largestSucessful, lastAttempt, maxBatchSize); }

        [Event(
            eventId: 16,
            Level = EventLevel.Informational,
            Message = "Reducing the batch size window down to {0} - {1}")]
        public void ReducingBatchSizes(int minBatchSize, int maxBatchSize)
        { WriteEvent(16, minBatchSize, maxBatchSize); }

        [Event(
            eventId: 17,
            Level = EventLevel.Error,
            Message = "Aborting - Too many consecutive errors.")]
        public void AbortingTooManyErrors()
        { WriteEvent(17); }
    }
}
