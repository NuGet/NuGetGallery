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

        /// <summary>
        /// Gets or sets the minimum download timestamp that will be replicated.
        /// </summary>
        /// <remarks>
        /// This value is inclusive.
        /// </remarks>
        public DateTime? MinTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the maximum download timestamp that will be replicated.
        /// Records will not be replicated unless there are downloads newer than
        /// the maximum download timestamp (indicating the boundary is cleared).
        /// </summary>
        /// <remarks>
        /// This value is exclusive so the upper boundary can be specified easily.
        /// </remarks>
        public DateTime? MaxTimestamp { get; set; }

        /// <summary>
        /// Whether or not to clear existing records when processing a bounded time window.
        /// </summary>
        public bool ClearExistingRecords { get; set; }

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

                MinTimestamp = JobConfigManager.TryGetDateTimeArgument(jobArgsDictionary, "MinTimestamp");
                MaxTimestamp = JobConfigManager.TryGetDateTimeArgument(jobArgsDictionary, "MaxTimestamp");
                ClearExistingRecords = JobConfigManager.TryGetBoolArgument(jobArgsDictionary, "Clear");

                return true;
            }
            catch(Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            JobEventSourceLog.ReplicatingStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            var count = await Replicate();
            watch.Stop();

            double perSecond = count / watch.Elapsed.TotalSeconds;
            JobEventSourceLog.ReplicatedStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, count, watch.Elapsed.TotalSeconds, (int)perSecond);

            return true;
        }

        private async Task<int> Replicate()
        {
            int totalReplicated = 0;

            // Get information about the source for the configured time window
            JobEventSourceLog.GettingSourceReplicationMarker(Source.DataSource, Source.InitialCatalog);
            var replicationSourceMarker = await GetReplicationSourceMarker(Source, MinTimestamp, MaxTimestamp);
            JobEventSourceLog.GotSourceReplicationMarker(replicationSourceMarker.MinKey, replicationSourceMarker.MaxKey, replicationSourceMarker.RecordsToReplicate, replicationSourceMarker.MinTimestamp, replicationSourceMarker.MaxTimestamp);

            // If we're replaying a time window (with both a min and max configured), then clear any existing records
            // that exist within that time window to start fresh.
            if (MinTimestamp.HasValue && MaxTimestamp.HasValue && ClearExistingRecords)
            {
                JobEventSourceLog.ClearingDownloadFacts(Destination.DataSource, Destination.InitialCatalog, MinTimestamp.Value, MaxTimestamp.Value);
                int factsCleared = await ClearDownloadFacts(Destination, MinTimestamp.Value, MaxTimestamp.Value);
                JobEventSourceLog.ClearedDownloadFacts(factsCleared);
            }

            // Using the time window from the source that has data, find the time window from the target where there's missing data
            JobEventSourceLog.GettingNextTargetTimeWindow(Destination.DataSource, Destination.InitialCatalog, replicationSourceMarker.MinTimestamp, replicationSourceMarker.MaxTimestamp);
            var replicationTargetMarker = await GetReplicationTargetMarker(Destination, replicationSourceMarker);
            JobEventSourceLog.GotNextTargetTimeWindow(replicationTargetMarker.MinTimestamp, replicationTargetMarker.MaxTimestamp, replicationTargetMarker.TimeWindowNeedsReplication ? "Replication Needed" : "No Replication Needed");

            if (replicationTargetMarker.TimeWindowNeedsReplication)
            {
                Stopwatch totalTime = new Stopwatch();
                totalTime.Start();

                do
                {
                    JobEventSourceLog.ReplicatingBatch();

                    double recordsPerSecond;
                    int batchSize = GetNextBatchSize(out recordsPerSecond);
                    int recordsRemaining = replicationSourceMarker.RecordsToReplicate - totalReplicated;
                    string timeRemaining = "<unknown>";

                    if (recordsPerSecond > 0)
                    {
                        double secondsRemaining = recordsRemaining / recordsPerSecond;
                        timeRemaining = TimeSpan.FromSeconds(secondsRemaining).ToString("g");
                    }

                    JobEventSourceLog.WorkRemaining(recordsRemaining, (int)recordsPerSecond, timeRemaining);

                    try
                    {
                        var watch = new Stopwatch();
                        watch.Start();
                        replicationTargetMarker = await ReplicateBatch(replicationSourceMarker, replicationTargetMarker, batchSize);
                        watch.Stop();

                        RecordSuccessfulBatchTime(replicationTargetMarker.LastBatchCount, watch.Elapsed);
                        totalReplicated += replicationTargetMarker.LastBatchCount;
                    }
                    catch (SqlException sqlException)
                    {
                        JobEventSourceLog.BatchFailed(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, batchSize, replicationSourceMarker.MaxKey, replicationTargetMarker.MaxKey, sqlException.ToString());

                        // If we can't even process the min batch size, then give up
                        if (batchSize <= MinBatchSize)
                        {
                            JobEventSourceLog.UnableToProcessMinimumBatchSize(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, batchSize, replicationSourceMarker.MaxKey, replicationTargetMarker.MaxKey);
                            throw;
                        }

                        // Otherwise, let's reduce our batch size range
                        ReduceBatchSizes(batchSize);
                    }

                    recordsPerSecond = totalReplicated / totalTime.Elapsed.TotalSeconds;
                    JobEventSourceLog.ReplicatedBatch(totalReplicated, TimeSpan.FromSeconds(totalTime.Elapsed.TotalSeconds).ToString("g"), (int)recordsPerSecond);
                }
                while (replicationTargetMarker.LastBatchCount > 0);
            }

            return totalReplicated;
        }

        private async Task<ReplicationTargetMarker> ReplicateBatch(ReplicationSourceMarker sourceMarker, ReplicationTargetMarker targetMarker, int batchSize)
        {
            JobEventSourceLog.FetchingStatisticsChunk(Source.DataSource, Source.InitialCatalog, batchSize);
            var batch = await GetDownloadRecords(Source, sourceMarker, targetMarker, batchSize);

            if (batch != null)
            {
                targetMarker.LastBatchCount = batch.Root.Nodes().Count();
            }
            else
            {
                targetMarker.LastBatchCount = 0;
            }

            JobEventSourceLog.FetchedStatisticsChunk(Source.DataSource, Source.InitialCatalog, targetMarker.LastBatchCount);

            if (targetMarker.LastBatchCount > 0)
            {
                JobEventSourceLog.SavingDownloadFacts(Destination.InitialCatalog, Destination.DataSource, targetMarker.LastBatchCount);
                int maxOriginalKey = await PutDownloadRecords(Destination, batch);
                JobEventSourceLog.SavedDownloadFacts(Destination.InitialCatalog, Destination.DataSource, targetMarker.LastBatchCount);

                if (maxOriginalKey > targetMarker.MaxKey)
                {
                    targetMarker.MaxKey = maxOriginalKey;
                }
                else
                {
                    targetMarker.LastBatchCount = 0;
                }
            }

            return targetMarker;
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
                recordsPerSecond = 100; // A baseline pace to expect
                JobEventSourceLog.UsingFirstSampleBatchSize(MinBatchSize, MaxBatchSize);
            }
            else if (BatchTimes.Count < 11)
            {
                // We'll run through 11 iterations of our possible range, with 10% increments along the way.
                // Yes, 11. Because fenceposts.
                KeyValuePair<double, int> bestSoFar = BatchTimes.OrderByDescending(batch => batch.Key).First();
                nextBatchSize = MinBatchSize + ((MaxBatchSize - MinBatchSize) / 10 * BatchTimes.Count);
                recordsPerSecond = bestSoFar.Key; // Optimistically, we'll match the best time after it all levels out
                JobEventSourceLog.UsingNextSampleBatchSize(BatchTimes.Count, nextBatchSize, bestSoFar.Value, (int)bestSoFar.Key);
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

        private void RecordSuccessfulBatchTime(int batchSize, TimeSpan elapsedTime)
        {
            double perSecond = batchSize / elapsedTime.TotalSeconds;
            BatchTimes[perSecond] = batchSize;

            JobEventSourceLog.SuccessfulBatch(batchSize, elapsedTime.TotalSeconds, (int)perSecond);
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

        private static async Task<ReplicationSourceMarker> GetReplicationSourceMarker(SqlConnectionStringBuilder source, DateTime? minTimestamp, DateTime? maxTimestamp)
        {
            string sql = @"
                SELECT		MIN([Key]) AS MinKey
		                ,	MAX([Key]) AS MaxKey
                        ,   MIN([Timestamp]) AS MinTimestamp
                        ,   MAX([Timestamp]) AS MaxTimestamp
                        ,   COUNT(*) AS Records
                FROM		PackageStatistics
                WHERE		[Timestamp] >= @minTimestamp
		                AND	[Timestamp] < @maxTimestamp";

            using (var connection = await source.ConnectTo())
            {
                SqlCommand command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@minTimestamp", minTimestamp ?? DateTime.MinValue);
                command.Parameters.AddWithValue("@maxTimestamp", maxTimestamp ?? DateTime.MaxValue);

                using (var result = await command.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.KeyInfo))
                {
                    if (result.HasRows && result.Read())
                    {
                        return new ReplicationSourceMarker
                        {
                            MinKey = result.GetInt32(result.GetOrdinal("MinKey")),
                            MaxKey = result.GetInt32(result.GetOrdinal("MaxKey")),
                            // Keep the original timestamp min/max values if specified
                            // Otherwise we lose the boundary values and might not process the window (thinking it's incomplete)
                            MinTimestamp = minTimestamp ?? result.GetDateTime(result.GetOrdinal("MinTimestamp")),
                            MaxTimestamp = maxTimestamp ?? result.GetDateTime(result.GetOrdinal("MaxTimestamp")),
                            RecordsToReplicate = result.GetInt32(result.GetOrdinal("Records"))
                        };
                    }
                }
            }

            return new ReplicationSourceMarker
            {
                MinKey = 0,
                MaxKey = 0
            };
        }

        private static async Task<ReplicationTargetMarker> GetReplicationTargetMarker(SqlConnectionStringBuilder target, ReplicationSourceMarker sourceMarker)
        {
            using (var connection = await target.ConnectTo())
            {
                using (var command = new SqlCommand("GetTimeWindowToProcess", connection))
                {
                    var minTimestamp = new SqlParameter("@minTimestamp", sourceMarker.MinTimestamp) { Direction = ParameterDirection.InputOutput };
                    var maxTimestamp = new SqlParameter("@maxTimestamp", sourceMarker.MaxTimestamp) { Direction = ParameterDirection.InputOutput };

                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(minTimestamp);
                    command.Parameters.Add(maxTimestamp);

                    await command.ExecuteNonQueryAsync();

                    // If the min/max pair is null then that means there are no records missing
                    // from the target. So we use the MaxTimestamp as the null value for BOTH
                    // as that will result in no records to replicate, but we also set the flag.
                    var minTimestampValue = (minTimestamp.Value as DateTime?) ?? sourceMarker.MaxTimestamp;
                    var maxTimestampValue = (maxTimestamp.Value as DateTime?) ?? sourceMarker.MaxTimestamp;

                    return new ReplicationTargetMarker
                    {
                        MinTimestamp = minTimestampValue,
                        MaxTimestamp = maxTimestampValue,
                        TimeWindowNeedsReplication = (minTimestampValue < maxTimestampValue)
                    };
                }
            }
        }

        private static async Task<int> ClearDownloadFacts(SqlConnectionStringBuilder target, DateTime minTimestamp, DateTime maxTimestamp)
        {
            using (var connection = await target.ConnectTo())
            {
                using (var command = new SqlCommand("ClearDownloadFacts", connection) { CommandTimeout = 60 * 30 }) // 30-minute timeout
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@minTimestamp", minTimestamp);
                    command.Parameters.AddWithValue("@maxTimestamp", maxTimestamp);

                    var recordsCleared = new SqlParameter() { Direction = ParameterDirection.ReturnValue };
                    command.Parameters.Add(recordsCleared);

                    await command.ExecuteNonQueryAsync();

                    return (int)recordsCleared.Value;
                }
            }
        }

        private static async Task<XDocument> GetDownloadRecords(SqlConnectionStringBuilder source, ReplicationSourceMarker sourceMarker, ReplicationTargetMarker targetMarker, int batchSize)
        {
            using (var connection = await source.ConnectTo())
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
                        WHERE PackageStatistics.[Key] >= @minSourceKey
                        AND   PackageStatistics.[Key] <= @maxSourceKey
                        AND   PackageStatistics.[Key] > @maxTargetKey
                        AND   PackageStatistics.[Timestamp] >= @minTimestamp
                        AND   PackageStatistics.[Timestamp] < @maxTimestamp
                        ORDER BY PackageStatistics.[Key]
                        FOR XML RAW('fact'), ELEMENTS, ROOT('facts')
                        ", connection))
                {
                    command.Parameters.AddWithValue("@batchSize", batchSize);
                    command.Parameters.AddWithValue("@minSourceKey", sourceMarker.MinKey);
                    command.Parameters.AddWithValue("@maxSourceKey", sourceMarker.MaxKey);
                    command.Parameters.AddWithValue("@maxTargetKey", targetMarker.MaxKey);
                    command.Parameters.AddWithValue("@minTimestamp", targetMarker.MinTimestamp);
                    command.Parameters.AddWithValue("@maxTimestamp", targetMarker.MaxTimestamp);

                    var factsReader = await command.ExecuteXmlReaderAsync();
                    var nodeType = factsReader.MoveToContent();

                    if (nodeType != XmlNodeType.None)
                    {
                        var factsDocument = XDocument.Load(factsReader);
                        return factsDocument;
                    }
                    else
                    {
                        // No data returned
                        return null;
                    }
                }
            }
        }

        private static async Task<int> PutDownloadRecords(SqlConnectionStringBuilder target, XDocument batch)
        {
            using (var connection = await target.ConnectTo())
            {
                using (var command = new SqlCommand("AddDownloadFacts", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@facts", batch.ToString());

                    var maxOriginalKey = new SqlParameter("@maxOriginalKey", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    command.Parameters.Add(maxOriginalKey);

                    await command.ExecuteNonQueryAsync();

                    return (int)maxOriginalKey.Value;
                }
            }
        }

        private struct ReplicationSourceMarker
        {
            public int MinKey;
            public int MaxKey;
            public DateTime MinTimestamp;
            public DateTime MaxTimestamp;
            public int RecordsToReplicate;
        }

        private struct ReplicationTargetMarker
        {
            public DateTime MinTimestamp;
            public DateTime MaxTimestamp;
            public bool TimeWindowNeedsReplication;
            public int MaxKey;
            public int LastBatchCount;
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
            Message = "===== Replicated {4:n0} records from {0}/{1} to {2}/{3}. Duration: {5}. Pace: {6}. =====")]
        public void ReplicatedStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase, int count, double seconds, int perSecond)
        { WriteEvent(2, sourceServer, sourceDatabase, destServer, destDatabase, count.ToString("#,###"), TimeSpan.FromSeconds(seconds).ToString(), perSecond); }

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
        public void UsingNextSampleBatchSize(int samplesTaken, int sampleSize, int bestSizeSoFar, int recordsPerSecond)
        { WriteEvent(12, samplesTaken, sampleSize, bestSizeSoFar, recordsPerSecond); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Message = "Calculated the batch size of {0} using the best of {1} batches. Best batch sizes so far: {2}, running at the following paces (per second): {3}")]
        public void UsingCalculatedBatchSize(int sampleSize, int timesRecorded, string bestBatchSizes, string bestBatchSizePaces)
        { WriteEvent(13, sampleSize, timesRecorded, bestBatchSizes, bestBatchSizePaces); }

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
        public void SuccessfulBatch(int batchSize, double elapsedSeconds, int perSecond)
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
            Message = "Records Remaining: {0:n0}. Optimistic Pace: {1}/second. Optimistic Time Remaining: {2}")]
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
            Message = "----- Batch Complete. Records processed so far: {0:n0}. Total time: {1}. Overall Pace: {2}/second. -----")]
        public void ReplicatedBatch(int totalCount, string elapsedTime, int recordsPerSecond)
        {
            WriteEvent(23, totalCount, elapsedTime, recordsPerSecond);
        }

        [Event(
            eventId: 24,
            Task = Tasks.GettingSourceReplicationMarker,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting replication source marker from {0}/{1}")]
        public void GettingSourceReplicationMarker(string server, string database)
        { WriteEvent(24, server, database); }

        [Event(
            eventId: 25,
            Task = Tasks.GettingSourceReplicationMarker,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "The replication source marker has the range of {0}-{1} ({2:n0} records) over the time window of {3} to {4}.")]
        public void GotSourceReplicationMarker(int minKey, int maxKey, int records, DateTime minTimestamp, DateTime maxTimestamp)
        { WriteEvent(25, minKey, maxKey, records, minTimestamp, maxTimestamp); }

        [Event(
            eventId: 26,
            Task = Tasks.ClearingDownloadFacts,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Clearing existing records from {0}/{1} for the specified time window of {2} to {3}.")]
        public void ClearingDownloadFacts(string server, string database, DateTime minTime, DateTime maxTime)
        { WriteEvent(26, server, database, minTime, maxTime); }

        [Event(
            eventId: 27,
            Task = Tasks.ClearingDownloadFacts,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Cleared {0} existing records.")]
        public void ClearedDownloadFacts(int factsCleared)
        { WriteEvent(27, factsCleared); }

        [Event(
            eventId: 28,
            Task = Tasks.GettingTargetTimeWindowToProcess,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting the next target time window (where no target records exist) from {0}/{1} within the range of {2} to {3} where the source has records.")]
        public void GettingNextTargetTimeWindow(string server, string database, DateTime minTimestamp, DateTime maxTimestamp)
        { WriteEvent(28, server, database, minTimestamp, maxTimestamp); }

        [Event(
            eventId: 29,
            Task = Tasks.GettingTargetTimeWindowToProcess,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Got the next target time window. Time window to process is {0} to {1}. {2}.")]
        public void GotNextTargetTimeWindow(DateTime minTimestamp, DateTime maxTimestamp, string status)
        { WriteEvent(29, minTimestamp, maxTimestamp, status); }

        public static class Tasks
        {
            public const EventTask ReplicatingStatistics = (EventTask)0x1;
            public const EventTask GettingLastReplicatedKey = (EventTask)0x2;
            public const EventTask FetchingStatisticsChunk = (EventTask)0x3;
            public const EventTask SavingDownloadFacts = (EventTask)0x4;
            public const EventTask ReplicatingBatch = (EventTask)0x6;
            public const EventTask GettingSourceReplicationMarker = (EventTask)0x7;
            public const EventTask ClearingDownloadFacts = (EventTask)0x8;
            public const EventTask GettingTargetTimeWindowToProcess = (EventTask)0x9;
        }
    }

}
