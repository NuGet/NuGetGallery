using NuGet.Jobs.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Stats.Replicator
{
    internal class Job : JobBase
    {
        private static readonly JobEventSource JobEventSourceLog = JobEventSource.Log;
        private static int MinBatchSize = 100;
        private static int MaxBatchSize = 10000;
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
                return true;
            }
            catch(Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task Run()
        {
            JobEventSourceLog.ReplicatingStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            var count = await Replicate();
            watch.Stop();

            double perSecond = count / watch.Elapsed.TotalSeconds;
            JobEventSourceLog.ReplicatedStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, count, watch.Elapsed.TotalSeconds, (int)perSecond);
        }

        private async Task<int> Replicate()
        {
            int total = 0;
            int replicated = 0;

            JobEventSourceLog.GettingMaxSourceKey(Source.DataSource, Source.InitialCatalog);
            var sourceMaxKey = await GetMaxSourceKey(Source);
            JobEventSourceLog.GotMaxSourceKey(Source.DataSource, Source.InitialCatalog, sourceMaxKey);

            Stopwatch totalTime = new Stopwatch();
            totalTime.Start();

            do
            {
                JobEventSourceLog.ReplicatingBatch();

                JobEventSourceLog.GettingLastReplicatedKey(Destination.DataSource, Destination.InitialCatalog);
                var targetMaxKey = await GetMaxTargetKey(Destination);
                JobEventSourceLog.GotLastReplicatedKey(Destination.DataSource, Destination.InitialCatalog, targetMaxKey);

                if (targetMaxKey >= sourceMaxKey)
                {
                    JobEventSourceLog.LastReplicatedKeyNotChanged();
                    break;
                }

                double recordsPerSecond;
                int batchSize = GetNextBatchSize(out recordsPerSecond);

                if (recordsPerSecond > 0)
                {
                    int recordsRemaining = sourceMaxKey - targetMaxKey;
                    double secondsRemaining = recordsRemaining / recordsPerSecond;
                    JobEventSourceLog.WorkRemaining(recordsRemaining, (int)recordsPerSecond, TimeSpan.FromSeconds(secondsRemaining).ToString("g"));
                }

                try
                {
                    var watch = new Stopwatch();
                    watch.Start();

                    replicated = await ReplicateBatch(targetMaxKey, batchSize);

                    watch.Stop();
                    RecordSuccessfulBatchTime(batchSize, watch.Elapsed);
                }
                catch (SqlException sqlException)
                {
                    JobEventSourceLog.BatchFailed(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, batchSize, sourceMaxKey, targetMaxKey, sqlException.ToString());

                    // If we can't even process the min batch size, then give up
                    if (batchSize <= MinBatchSize)
                    {
                        JobEventSourceLog.UnableToProcessMinimumBatchSize(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, batchSize, sourceMaxKey, targetMaxKey);
                        throw;
                    }

                    // Otherwise, let's reduce our batch size range
                    ReduceBatchSizes(batchSize);
                }

                if (replicated > 0)
                {
                    total += replicated;
                }

                recordsPerSecond = total / totalTime.Elapsed.TotalSeconds;
                JobEventSourceLog.ReplicatedBatch(total, TimeSpan.FromSeconds(totalTime.Elapsed.TotalSeconds).ToString("g"), (int)recordsPerSecond);
            }
            while (replicated > 0);

            return total;
        }


        private void RecordSuccessfulBatchTime(int batchSize, TimeSpan elapsedTime)
        {
            double perSecond = batchSize / elapsedTime.TotalSeconds;
            BatchTimes[perSecond] = batchSize;

            JobEventSourceLog.SuccessfulBatch(batchSize, elapsedTime.TotalSeconds, perSecond);
        }

        private void ReduceBatchSizes(int batchSize)
        {
            if (BatchTimes.Any())
            {
                int maxSuccessfulMatch = BatchTimes.Values.Max();

                if (MaxBatchSize > maxSuccessfulMatch)
                {
                    // Split the difference between the max successful batch size and our batch size that just failed
                    MaxBatchSize = (maxSuccessfulMatch + batchSize) / 2;
                }
                else
                {
                    MaxBatchSize = MaxBatchSize * 2 / 3;
                }

                // Ensure the Max doesn't fall below the Min
                MaxBatchSize = Math.Max(MaxBatchSize, MinBatchSize);
                JobEventSourceLog.CappingMaxBatchSize(maxSuccessfulMatch, batchSize, MaxBatchSize);
            }
            else
            {
                MinBatchSize = MinBatchSize / 2;
                MaxBatchSize = MaxBatchSize * 2 / 3;

                // Ensure the Max doesn't fall below the Min
                MaxBatchSize = Math.Max(MaxBatchSize, MinBatchSize);
                JobEventSourceLog.ReducingBatchSizes(MinBatchSize, MaxBatchSize);
            }
        }

        private async Task<int> ReplicateBatch(int originalKey, int batchSize)
        {
            JobEventSourceLog.FetchingStatisticsChunk(Source.DataSource, Source.InitialCatalog, batchSize);
            var batch = await GetDownloadRecords(originalKey, batchSize);
            var batchCount = batch.Root.Nodes().Count();
            JobEventSourceLog.FetchedStatisticsChunk(Source.DataSource, Source.InitialCatalog, batchCount);

            if (batchCount > 0)
            {
                JobEventSourceLog.SavingDownloadFacts(Destination.InitialCatalog, Destination.DataSource, batchCount);
                // Should consider enabling WhatIf
                //if (!WhatIf)
                {
                    await PutDownloadRecords(batch, originalKey);
                }
                JobEventSourceLog.SavedDownloadFacts(Destination.InitialCatalog, Destination.DataSource, batchCount);
            }

            return batchCount;
        }

        public static async Task<int> GetMaxSourceKey(SqlConnectionStringBuilder connectionString)
        {
            using (var connection = await connectionString.ConnectTo())
            {
                SqlCommand command = new SqlCommand("SELECT MAX([Key]) AS MaxOriginalKey FROM PackageStatistics", connection);
                int? maxOriginalKey = await command.ExecuteScalarAsync() as int?;

                return maxOriginalKey ?? -1;
            }
        }

        public static async Task<int> GetMaxTargetKey(SqlConnectionStringBuilder connectionString)
        {
            using (var connection = await connectionString.ConnectTo())
            {
                SqlCommand command = new SqlCommand("GetLastOriginalKey", connection) { CommandType = CommandType.StoredProcedure };
                SqlParameter param = new SqlParameter("@OriginalKey", SqlDbType.Int) { Direction = ParameterDirection.Output };
                command.Parameters.Add(param);

                await command.ExecuteNonQueryAsync();
                int? maxOriginalKey = param.Value as int?;

                return maxOriginalKey ?? -1;
            }
        }

        private async Task<XDocument> GetDownloadRecords(int originalKey, int batchSize)
        {
            using (var connection = await Source.ConnectTo())
            {
                using (var command = new SqlCommand(@"
                        SELECT TOP(@batchSize) 
                            PackageStatistics.[Key] 'originalKey', 
                            PackageRegistrations.[Id] 'packageId', 
                            Packages.[Version] 'packageVersion', 
	                        Packages.[Listed] 'packageListed',
                            Packages.[Title] 'packageTitle',
                            Packages.[Description] 'packageDescription',
                            Packages.[IconUrl] 'packageIconUrl',
                            ISNULL(PackageStatistics.[UserAgent], '') 'downloadUserAgent', 
                            ISNULL(PackageStatistics.[Operation], '') 'downloadOperation', 
                            PackageStatistics.[Timestamp] 'downloadTimestamp',
                            PackageStatistics.[ProjectGuids] 'downloadProjectTypes',
                            PackageStatistics.[DependentPackage] 'downloadDependentPackageId'
                        FROM PackageStatistics 
                        INNER JOIN Packages ON PackageStatistics.PackageKey = Packages.[Key] 
                        INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.PackageRegistrationKey 
                        WHERE PackageStatistics.[Key] > @originalKey 
                        ORDER BY PackageStatistics.[Key]
                        FOR XML RAW('fact'), ELEMENTS, ROOT('facts')
                        ", connection))
                {
                    command.Parameters.Add(new SqlParameter("@originalKey", originalKey));
                    command.Parameters.Add(new SqlParameter("@batchSize", batchSize));

                    var factsReader = await command.ExecuteXmlReaderAsync();
                    var factsDocument = XDocument.Load(factsReader);

                    return factsDocument;
                }
            }
        }

        private async Task PutDownloadRecords(XDocument batch, int expectedLastOriginalKey)
        {
            using (var connection = await Destination.ConnectTo())
            {
                using (var command = new SqlCommand("AddDownloadFacts", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@facts", batch.ToString()));
                    command.Parameters.Add(new SqlParameter("@expectedLastOriginalKey", expectedLastOriginalKey));
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private int GetNextBatchSize(out double recordsPerSecond)
        {
            // Every 100 runs, we will reset our time recordings and find a new best time all over
            if (BatchTimes.Count >= 100)
            {
                BatchTimes.Clear();
            }

            int nextBatchSize;

            if (BatchTimes.Count == 0)
            {
                nextBatchSize = MinBatchSize;
                recordsPerSecond = 0;
                JobEventSourceLog.UsingFirstSampleBatchSize(MinBatchSize, MaxBatchSize);
            }
            else if (BatchTimes.Count < 11)
            {
                // We'll run through 11 iterations of our possible range, with 10% increments along the way.
                // Yes, 11. Because fenceposts.
                KeyValuePair<double, int> bestSoFar = BatchTimes.OrderByDescending(batch => batch.Key).First();
                nextBatchSize = MinBatchSize + ((MaxBatchSize - MinBatchSize) / 10 * BatchTimes.Count);
                recordsPerSecond = bestSoFar.Key; // Optimistically, we'll match the best time after it all levels out
                JobEventSourceLog.UsingNextSampleBatchSize(BatchTimes.Count, nextBatchSize, bestSoFar.Value, bestSoFar.Key);
            }
            else
            {
                IEnumerable<KeyValuePair<double, int>> bestBatches = BatchTimes.OrderByDescending(batch => batch.Key).Take(BatchTimes.Count / 4);
                string bestSizes = String.Join(", ", bestBatches.Select(batch => batch.Value));
                string bestPaces = String.Join(", ", bestBatches.Select(batch => (int)batch.Key));

                nextBatchSize = (int)bestBatches.Select(batch => batch.Value).Average();
                recordsPerSecond = bestBatches.First().Key; // Optimistically, we'll match the best time
                JobEventSourceLog.UsingCalculatedBatchSize(nextBatchSize, BatchTimes.Count, bestSizes, bestPaces);
            }

            // Ensure the next batch size is within the allowable range
            return Math.Max(Math.Min(nextBatchSize, MaxBatchSize), MinBatchSize);
        }

    }

    [EventSource(Name = "Outercurve-NuGet-Jobs-ReplicatePackageStatistics")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();
        private JobEventSource() { }

        [Event(
            eventId: 1,
            Task = Tasks.ReplicatingStatistics,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Replicating statistics from {0}/{1} to {2}/{3}")]
        public void ReplicatingStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase)
        { WriteEvent(1, sourceServer, sourceDatabase, destServer, destDatabase); }

        [Event(
            eventId: 2,
            Task = Tasks.ReplicatingStatistics,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "===== Replicated {4} records from {0}/{1} to {2}/{3}. Duration: {5}. Pace: {6}. =====")]
        public void ReplicatedStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase, int count, double seconds, int perSecond)
        { WriteEvent(2, sourceServer, sourceDatabase, destServer, destDatabase, count.ToString("#,###"), TimeSpan.FromSeconds(seconds).ToString(), perSecond); }

        [Event(
            eventId: 3,
            Task = Tasks.GettingLastReplicatedKey,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting last replicated key from {0}/{1}")]
        public void GettingLastReplicatedKey(string server, string database)
        { WriteEvent(3, server, database); }

        [Event(
            eventId: 4,
            Task = Tasks.GettingLastReplicatedKey,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Last replicated key from {0}/{1} is {2}")]
        public void GotLastReplicatedKey(string server, string database, int key)
        { WriteEvent(4, server, database, key); }

        [Event(
            eventId: 5,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Fetching {2} statistics entries from {0}/{1}")]
        public void FetchingStatisticsChunk(string server, string database, int limit)
        { WriteEvent(5, server, database, limit); }

        [Event(
            eventId: 6,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Fetched {2} statistics entries from {0}/{1}")]
        public void FetchedStatisticsChunk(string server, string database, int count)
        { WriteEvent(6, server, database, count); }

        [Event(
            eventId: 7,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Saving {2} records to {0}/{1}")]
        public void SavingDownloadFacts(string server, string database, int count)
        { WriteEvent(7, server, database, count); }

        [Event(
            eventId: 8,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Saved {2} records to {0}/{1}")]
        public void SavedDownloadFacts(string server, string database, int count)
        { WriteEvent(8, server, database, count); }

        [Event(
            eventId: 9,
            Task = Tasks.GettingLastReplicatedKey,
            Level = EventLevel.Warning,
            Message = "Last replicated key has not changed meaning no data was inserted last run. Stopping")]
        public void LastReplicatedKeyNotChanged()
        { WriteEvent(9); }

        /* *****************************
         * Event Id 10 used to exist
         * It was 'SlowQueryInfo'
         * *****************************/

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Message = "Sampling batch sizes. Min batch size: {0}; Max batch size: {1}")]
        public void UsingFirstSampleBatchSize(int minBatch, int maxBatch)
        { WriteEvent(11, minBatch, maxBatch); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Message = "Sampling batch sizes. Samples taken: {0}; Next sample size: {1}; Best sample size so far: {2} at {3} records per second")]
        public void UsingNextSampleBatchSize(int samplesTaken, int sampleSize, int bestSizeSoFar, double factsPerSecond)
        { WriteEvent(12, samplesTaken, sampleSize, bestSizeSoFar, factsPerSecond); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Message = "Calculated the batch size of {0} using the best of {1} batches. Best batch sizes so far: {2}, running at the following paces (per second): {3}")]
        public void UsingCalculatedBatchSize(int sampleSize, int timesRecorded, string bestBatchSizes, string bestBatchSizePaces)
        { WriteEvent(13, sampleSize, timesRecorded, bestBatchSizes, bestBatchSizePaces); }

        [Event(
            eventId: 14,
            Task = Tasks.GettingMaxSourceKey,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting max source key key from {0}/{1}")]
        public void GettingMaxSourceKey(string server, string database)
        { WriteEvent(14, server, database); }

        [Event(
            eventId: 15,
            Task = Tasks.GettingMaxSourceKey,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "The max source key from {0}/{1} is {2}")]
        public void GotMaxSourceKey(string server, string database, int key)
        { WriteEvent(15, server, database, key); }

        [Event(
            eventId: 16,
            Level = EventLevel.Warning,
            Message = "An error occurring replicating a batch. Source: {0}/{1}. Destination: {2}/{3}. Batch Size: {4}. Source Max Original Key: {5}; Destination Max Original Key: {6}. Exception: {7}")]
        public void BatchFailed(string sourceServer, string sourceDatabase, string destinationServer, string destinationDatabase, int batchSize, int sourceMaxKey, int destinationMaxKey, string exception)
        { WriteEvent(16, sourceServer, sourceDatabase, destinationServer, destinationDatabase, batchSize, sourceMaxKey, destinationMaxKey, exception); }

        [Event(
            eventId: 17,
            Level = EventLevel.Informational,
            Message = "Capping the max batch size to the average of the largest successful batch size of {0} and the last attempted batch size of {1}. New max batch size is {2}.")]
        public void CappingMaxBatchSize(int largestSucessful, int lastAttempt, int maxBatchSize)
        { WriteEvent(17, largestSucessful, lastAttempt, maxBatchSize); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Message = "Reducing the batch size window down to {0} - {1}")]
        public void ReducingBatchSizes(int minBatchSize, int maxBatchSize)
        { WriteEvent(18, minBatchSize, maxBatchSize); }

        [Event(
            eventId: 19,
            Level = EventLevel.Informational,
            Message = "Batch of {0} records succeeded in {1} seconds ({2}/second)")]
        public void SuccessfulBatch(int batchSize, double elapsedSeconds, double perSecond)
        { WriteEvent(19, batchSize, elapsedSeconds, perSecond); }

        [Event(
            eventId: 20,
            Level = EventLevel.Error,
            Message = "Aborting - Unable to process minimum batch size. Source: {0}/{1}. Destination: {2}/{3}. Batch Size: {4}. Source Max Original Key: {5}; Destination Max Original Key: {6}")]
        public void UnableToProcessMinimumBatchSize(string sourceServer, string sourceDatabase, string destinationServer, string destinationDatabase, int batchSize, int sourceMaxKey, int destinationMaxKey)
        { WriteEvent(20, sourceServer, sourceDatabase, destinationServer, destinationDatabase, batchSize, sourceMaxKey, destinationMaxKey); }

        [Event(
            eventId: 21,
            Level = EventLevel.Informational,
            Message = "Records Remaining: {0}. Optimistic Pace: {1}/second. Optimistic Time Remaining: {2}")]
        public void WorkRemaining(int recordsRemaining, int recordsPerSecond, string timeRemaining)
        {
            WriteEvent(21, recordsRemaining, recordsPerSecond, timeRemaining);
        }

        [Event(
            eventId: 22,
            Level = EventLevel.Informational,
            Task = Tasks.ReplicatingBatch,
            Opcode = EventOpcode.Start,
            Message = "----- Batch Starting -----")]
        public void ReplicatingBatch()
        { WriteEvent(22); }

        [Event(
            eventId: 23,
            Level = EventLevel.Informational,
            Task = Tasks.ReplicatingBatch,
            Opcode = EventOpcode.Stop,
            Message = "----- Batch Complete. Records processed so far: {0}. Total time: {1}. Overall Pace: {2}/second. -----")]
        public void ReplicatedBatch(int totalCount, string elapsedTime, int recordsPerSecond)
        {
            WriteEvent(23, totalCount, elapsedTime, recordsPerSecond);
        }

        public static class Tasks
        {
            public const EventTask ReplicatingStatistics = (EventTask)0x1;
            public const EventTask GettingLastReplicatedKey = (EventTask)0x2;
            public const EventTask FetchingStatisticsChunk = (EventTask)0x3;
            public const EventTask SavingDownloadFacts = (EventTask)0x4;
            public const EventTask GettingMaxSourceKey = (EventTask)0x5;
            public const EventTask ReplicatingBatch = (EventTask)0x6;
        }
    }

}
