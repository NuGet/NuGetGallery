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
        private const int MinBatchSize = 100;
        private const int MaxBatchSize = 10000;
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
            JobEventSourceLog.GotReplicationCursor();

            if (minTimestampToKeep == null)
            {
                return 0;
            }

            JobEventSourceLog.PurgingStatistics(Source.DataSource, Source.InitialCatalog);
            var watch = new Stopwatch();
            watch.Start();
            int purged = await PurgeCore(minTimestampToKeep.Value);
            watch.Stop();
            JobEventSourceLog.PurgedStatistics(purged, watch.Elapsed.TotalSeconds);
        }

        private async Task<DateTime?> GetMinTimestampToKeep(SqlConnectionStringBuilder Destination)
        {
            // Get the most recent cursor window that is older than the days we want to keep.
            // By getting the MAX(MinTimestamp), we'll delete statistics older than the beginning of the
            // most recent window that has begun processing (but isn't guaranteed to have completed).
            var sql = @"
                SELECT      MAX(MinTimestamp)
                FROM        CollectorCursor
                WHERE       MinTimestamp <= DATEADD(day, @DaysToKeep, convert(date, GETUTCDATE()))";

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
                JobEventSourceLog.PurgingBatch();

                double recordsPerSecond;
                int batchSize = GetNextBatchSize(out recordsPerSecond);
                int purged = 0;

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
                }
                catch
                {
                    JobEventSourceLog.BatchFailed(batchSize);

                    // If we can't even process the min batch size, then give up
                    if (batchSize <= CurrentMinBatchSize)
                    {
                        JobEventSourceLog.UnableToProcessMinimumBatchSize(batchSize);
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

            using (var connection = await Destination.ConnectTo())
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

        internal void PurgingReplicatedStatistics(string p1, string p2, string p3, string p4)
        {
            throw new NotImplementedException();
        }

        internal void PurgedReplicatedStatistics(var count, double p1, int p2)
        {
            throw new NotImplementedException();
        }

        internal void NoStatisticsToPurge(double p)
        {
            throw new NotImplementedException();
        }

        internal void GettingReplicationCursor(string p1, string p2)
        {
            throw new NotImplementedException();
        }

        internal void GotReplicationCursor()
        {
            throw new NotImplementedException();
        }

        internal void PurgingStatistics(string p1, string p2)
        {
            throw new NotImplementedException();
        }

        internal void PurgedStatistics(int purged, double p)
        {
            throw new NotImplementedException();
        }
    }
}
