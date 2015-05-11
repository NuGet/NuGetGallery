// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
            catch (Exception ex)
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

            if (count > 0)
            {
                double perSecond = count / watch.Elapsed.TotalSeconds;
                JobEventSourceLog.ReplicatedStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, count, watch.Elapsed.TotalSeconds, (int)perSecond);
            }

            // In case we're running continuously, modify some parameters for the next run
            MinTimestamp = MaxTimestamp;
            ClearExistingRecords = false;
            return true;
        }

        private async Task<int> Replicate()
        {
            CurrentMinBatchSize = MinBatchSize;
            CurrentMaxBatchSize = MaxBatchSize;

            int totalReplicated = 0;
            int failures = 0;
            ReplicationSourceMarker replicationSourceMarker;
            ReplicationTargetMarker replicationTargetMarker;

            while (true)
            {
                try
                {
                    // Get information about the source for the configured time window
                    JobEventSourceLog.GettingSourceReplicationMarker();
                    replicationSourceMarker = await GetReplicationSourceMarker(Source, MinTimestamp, MaxTimestamp);

                    if (replicationSourceMarker.RecordsToReplicate > 0)
                    {
                        JobEventSourceLog.GotSourceReplicationMarker(replicationSourceMarker.MinKey, replicationSourceMarker.MaxKey, replicationSourceMarker.RecordsToReplicate, replicationSourceMarker.MinTimestamp, replicationSourceMarker.MaxTimestamp);
                        break;
                    }
                    else
                    {
                        JobEventSourceLog.NothingToReplicate();
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    JobEventSourceLog.ErrorOccurred(++failures, MaxFailures, ex.ToString());

                    if (failures >= MaxFailures)
                    {
                        throw;
                    }
                }
            }

            // If we're replaying a time window (with both a min and max configured), then clear any existing records
            // that exist within that time window to start fresh.
            if (MinTimestamp.HasValue && ClearExistingRecords)
            {
                failures = 0;

                while (true)
                {
                    try
                    {
                        await ClearDownloadFacts(Destination, MinTimestamp.Value, MaxTimestamp ?? replicationSourceMarker.MaxTimestamp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        JobEventSourceLog.ErrorOccurred(++failures, MaxFailures, ex.ToString());

                        if (failures >= MaxFailures)
                        {
                            throw;
                        }
                    }
                }
            }

            failures = 0;

            while (true)
            {
                try
                {
                    // Using the time window from the source that has data, find the time window from the target where there's missing data
                    JobEventSourceLog.GettingNextTargetTimeWindow(replicationSourceMarker.MinTimestamp, replicationSourceMarker.MaxTimestamp);
                    replicationTargetMarker = await GetReplicationTargetMarker(Destination, replicationSourceMarker);
                    JobEventSourceLog.GotNextTargetTimeWindow(replicationTargetMarker.MinTimestamp, replicationTargetMarker.MaxTimestamp, replicationTargetMarker.TimeWindowNeedsReplication ? "Replication Needed" : "No Replication Needed");

                    break;
                }
                catch (Exception ex)
                {
                    JobEventSourceLog.ErrorOccurred(++failures, MaxFailures, ex.ToString());

                    if (failures >= MaxFailures)
                    {
                        throw;
                    }
                }
            }

            failures = 0;

            while (true)
            {
                try
                {
                    if (replicationSourceMarker.MinTimestamp != replicationTargetMarker.MinTimestamp || replicationSourceMarker.MaxTimestamp != replicationTargetMarker.MaxTimestamp)
                    {
                        // Now refresh our source marker to be within the bounds of the target marker to be crystal clear about what we're replicating
                        // This prevents us from replicating an incomplete hour (more specifically, the current hour)
                        JobEventSourceLog.GettingSourceReplicationMarker();
                        replicationSourceMarker = await GetReplicationSourceMarker(Source, replicationTargetMarker.MinTimestamp, replicationTargetMarker.MaxTimestamp);

                        if (replicationSourceMarker.RecordsToReplicate > 0)
                        {
                            JobEventSourceLog.GotSourceReplicationMarker(replicationSourceMarker.MinKey, replicationSourceMarker.MaxKey, replicationSourceMarker.RecordsToReplicate, replicationSourceMarker.MinTimestamp, replicationSourceMarker.MaxTimestamp);
                            break;
                        }
                        else
                        {
                            JobEventSourceLog.NothingToReplicate();
                            return 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    JobEventSourceLog.ErrorOccurred(++failures, MaxFailures, ex.ToString());

                    if (failures >= MaxFailures)
                    {
                        throw;
                    }
                }
            }

            Stopwatch totalTime = new Stopwatch();
            totalTime.Start();

            while (replicationTargetMarker.TimeWindowNeedsReplication)
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

                var watch = new Stopwatch();
                watch.Start();
                replicationTargetMarker = await ReplicateBatch(replicationSourceMarker, replicationTargetMarker, batchSize);
                watch.Stop();

                if (!replicationTargetMarker.TimeWindowNeedsReplication)
                {
                    JobEventSourceLog.NothingToReplicate();
                    break;
                }
                else if (replicationTargetMarker.LastBatchCount > 0)
                {
                    // The batch succeeded
                    RecordSuccessfulBatchTime(replicationTargetMarker.LastBatchCount, watch.Elapsed);
                    totalReplicated += replicationTargetMarker.LastBatchCount;
                }
                else
                {
                    JobEventSourceLog.BatchFailed(batchSize, replicationSourceMarker.MaxKey, replicationTargetMarker.Cursor.HasValue ? replicationTargetMarker.Cursor.ToString() : "<null>");

                    // If we can't even process the min batch size, then give up
                    if (batchSize <= CurrentMinBatchSize)
                    {
                        JobEventSourceLog.UnableToProcessMinimumBatchSize(batchSize, replicationSourceMarker.MaxKey, replicationTargetMarker.Cursor.HasValue ? replicationTargetMarker.Cursor.ToString() : "<null>");
                        break;
                    }

                    // Otherwise, let's reduce our batch size range
                    RecordFailedBatchSize(batchSize);
                }

                recordsPerSecond = totalReplicated / totalTime.Elapsed.TotalSeconds;
                JobEventSourceLog.ReplicatedBatch(totalReplicated, TimeSpan.FromSeconds(totalTime.Elapsed.TotalSeconds).ToString("g"), (int)recordsPerSecond);
            }

            return totalReplicated;
        }

        private async Task<ReplicationTargetMarker> ReplicateBatch(ReplicationSourceMarker sourceMarker, ReplicationTargetMarker targetMarker, int batchSize)
        {
            targetMarker.LastBatchCount = 0;

            try
            {
                JobEventSourceLog.FetchingStatisticsChunk(batchSize);
                var batch = await GetDownloadRecords(Source, sourceMarker, targetMarker, batchSize);
                JobEventSourceLog.FetchedStatisticsChunk();

                // If there's nothing else to process, then return the specified target marker,
                // indicating we're done.
                if (batch == null || !batch.Descendants("fact").Any())
                {
                    targetMarker.TimeWindowNeedsReplication = false;
                    return targetMarker;
                }

                JobEventSourceLog.VerifyingCursor(targetMarker.MinTimestamp, targetMarker.MaxTimestamp, targetMarker.Cursor.HasValue ? targetMarker.Cursor.ToString() : "<null>");
                var cursor = await GetTargetCursor(Destination, targetMarker);

                if (cursor != targetMarker.Cursor)
                {
                    throw new InvalidOperationException(String.Format("Expected cursor for {0} to {1} to have the value of {2} but it had the value for {3}. Aborting.", targetMarker.MinTimestamp, targetMarker.MaxTimestamp, targetMarker.Cursor.HasValue ? targetMarker.Cursor.ToString() : "<null>", cursor.HasValue ? cursor.ToString() : "<null>"));
                }

                JobEventSourceLog.VerifiedCursor();

                // Determine what our new cursor value should be after completing this batch
                var newCursor = new ReplicationTargetMarker
                {
                    MinTimestamp = targetMarker.MinTimestamp,
                    MaxTimestamp = targetMarker.MaxTimestamp,
                    TimeWindowNeedsReplication = targetMarker.TimeWindowNeedsReplication,
                    LastBatchCount = batch.Root.Nodes().Count(),
                    Cursor = (from fact in batch.Descendants("fact")
                              let originalKey = (int)fact.Element("originalKey")
                              orderby originalKey descending
                              select originalKey).First()
                };

                var minBatchTime = batch.Descendants("fact").Min(f => DateTime.Parse(f.Element("downloadTimestamp").Value));
                var maxBatchTime = batch.Descendants("fact").Max(f => DateTime.Parse(f.Element("downloadTimestamp").Value));

                JobEventSourceLog.SavingDownloadFacts(newCursor.LastBatchCount, minBatchTime, maxBatchTime);

                SqlException potentialException = null;

                try
                {
                    await PutDownloadRecords(Destination, batch, targetMarker, newCursor);
                }
                catch (SqlException sqlException)
                {
                    // If we got an exception, it's possible that the batch was still committed.
                    // Capture the exception in case we decide to throw it because the batch failed.
                    potentialException = sqlException;
                }

                // See if our new cursor was committed
                JobEventSourceLog.CheckingCursor();
                var committedCursor = await GetTargetCursor(Destination, newCursor);
                JobEventSourceLog.CheckedCursor(committedCursor.HasValue ? committedCursor.Value.ToString() : "<null>");

                if (potentialException != null)
                {
                    // An exception occurred. It's possible that the batch actually succeeded though.
                    if (committedCursor == newCursor.Cursor)
                    {
                        // Yep, the batch actually succeeded despite the reported exception
                        // A known scenarios for this is when a timeout is reported but the
                        // batch is actually committed
                        JobEventSourceLog.RecoveredFromErrorSavingDownloadFacts(targetMarker.MinTimestamp, targetMarker.MaxTimestamp, targetMarker.Cursor.HasValue ? targetMarker.Cursor.Value.ToString() : "<null>", newCursor.Cursor.HasValue ? newCursor.Cursor.Value.ToString() : "<null>", committedCursor.HasValue ? committedCursor.Value.ToString() : "<null>", potentialException.ToString());
                    }
                    else if (committedCursor == targetMarker.Cursor)
                    {
                        // Nope, the batch actually failed. Re-throw the exception, and we'll try
                        // to recover by retrying up to the max failure count.
                        throw potentialException;
                    }
                }

                if (committedCursor != newCursor.Cursor)
                {
                    // We didn't get an exception, but our committed cursor doesn't match expectations
                    // Let's abort because we don't know what just happened
                    throw new InvalidOperationException(String.Format("Expected cursor for {0} to {1} to have the value of {2} but it had the value for {3}. Aborting.", newCursor.MinTimestamp, newCursor.MaxTimestamp, newCursor.Cursor.HasValue ? newCursor.Cursor.ToString() : "<null>", committedCursor.HasValue ? committedCursor.Value.ToString() : "<null>"));
                }

                JobEventSourceLog.SavedDownloadFacts(newCursor.LastBatchCount);
                CurrentFailures = 0;
                return newCursor;
            }
            catch (SqlException exception)
            {
                // We will ignore failures up to the max failure count, at which time we abort.
                if (++CurrentFailures == MaxFailures)
                {
                    throw;
                }

                JobEventSourceLog.RecoveredFromFailedBatch(CurrentFailures, MaxFailures, exception.ToString());
                return targetMarker;
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
                command.Parameters.AddWithValue("@minTimestamp", minTimestamp ?? System.Data.SqlTypes.SqlDateTime.MinValue);
                command.Parameters.AddWithValue("@maxTimestamp", maxTimestamp ?? System.Data.SqlTypes.SqlDateTime.MaxValue);

                using (var result = await command.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.KeyInfo))
                {
                    if (result.HasRows && result.Read())
                    {
                        if (!result.IsDBNull(result.GetOrdinal("MinKey")) && !result.IsDBNull(result.GetOrdinal("MaxKey")))
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
                        else
                        {
                            return new ReplicationSourceMarker { RecordsToReplicate = 0 };
                        }
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
                using (var command = new SqlCommand("CreateCursor", connection))
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

        private static async Task<int?> GetTargetCursor(SqlConnectionStringBuilder target, ReplicationTargetMarker targetMarker)
        {
            using (var connection = await target.ConnectTo())
            {
                using (var command = new SqlCommand("GetCursor", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@minTimestamp", targetMarker.MinTimestamp);
                    command.Parameters.AddWithValue("@maxTimestamp", targetMarker.MaxTimestamp);

                    return await command.ExecuteScalarAsync() as int?;
                }
            }
        }

        private static async Task ClearDownloadFacts(SqlConnectionStringBuilder target, DateTime minTimestamp, DateTime maxTimestamp)
        {
            int totalRecordsCleared = 0;
            int recordsClearedInBatch = 0;

            do
            {
                JobEventSourceLog.ClearingDownloadFacts(minTimestamp, maxTimestamp);

                using (var connection = await target.ConnectTo())
                {
                    // This proc will delete 5000 records at a time, so we have to run in a loop until there's nothing more to delete
                    using (var command = new SqlCommand("ClearDownloadFacts", connection) { CommandTimeout = 60 * 30 }) // 30-minute timeout
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@minTimestamp", minTimestamp);
                        command.Parameters.AddWithValue("@maxTimestamp", maxTimestamp);

                        var recordsCleared = new SqlParameter() { Direction = ParameterDirection.ReturnValue };
                        command.Parameters.Add(recordsCleared);

                        await command.ExecuteNonQueryAsync();

                        recordsClearedInBatch = (int)recordsCleared.Value;
                        totalRecordsCleared += recordsClearedInBatch;
                    }
                }

                JobEventSourceLog.ClearedDownloadFacts(recordsClearedInBatch, "Batch Completed.");
            }
            while (recordsClearedInBatch == 5000); // Hard-coded to match the stored proc - allows us to stop when done

            JobEventSourceLog.ClearedDownloadFacts(totalRecordsCleared, "Finished.");
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
                        AND   PackageStatistics.[Timestamp] >= @minTimestamp
                        AND   PackageStatistics.[Timestamp] < @maxTimestamp
                        AND   PackageStatistics.[Key] > @cursor
                        ORDER BY PackageStatistics.[Key]
                        FOR XML RAW('fact'), ELEMENTS, ROOT('facts')
                        ", connection))
                {
                    command.Parameters.AddWithValue("@batchSize", batchSize);
                    command.Parameters.AddWithValue("@minSourceKey", sourceMarker.MinKey);
                    command.Parameters.AddWithValue("@maxSourceKey", sourceMarker.MaxKey);
                    command.Parameters.AddWithValue("@cursor", targetMarker.Cursor ?? 0);
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

        private static async Task PutDownloadRecords(SqlConnectionStringBuilder target, XDocument batch, ReplicationTargetMarker currentCursor, ReplicationTargetMarker newCursor)
        {
            using (var connection = await target.ConnectTo())
            {
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand("AddDownloadFacts", connection, transaction))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@facts", batch.ToString());
                        command.Parameters.AddWithValue("@cursorMinTimestamp", currentCursor.MinTimestamp);
                        command.Parameters.AddWithValue("@cursorMaxTimestamp", currentCursor.MaxTimestamp);
                        command.Parameters.AddWithValue("@cursor", newCursor.Cursor);

                        await command.ExecuteNonQueryAsync();
                        transaction.Commit();
                    }
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
            public int? Cursor;
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
            Message = "===== Replicated {4:n0} records from {0}/{1} to {2}/{3}. Duration: {5}. Pace: {6}/second. =====")]
        public void ReplicatedStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase, int count, double seconds, int perSecond)
        { WriteEvent(2, sourceServer, sourceDatabase, destServer, destDatabase, count.ToString("#,###"), TimeSpan.FromSeconds(seconds).ToString(), perSecond); }

        [Event(
            eventId: 5,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Fetching up to {0} statistics entries.")]
        public void FetchingStatisticsChunk(int limit)
        { WriteEvent(5, limit); }

        [Event(
            eventId: 6,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Done fetching statistics")]
        public void FetchedStatisticsChunk()
        { WriteEvent(6); }

        [Event(
            eventId: 7,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Saving {0} records from {1} to {2}")]
        public void SavingDownloadFacts(int count, DateTime minBatchTime, DateTime maxBatchTime)
        { WriteEvent(7, count, minBatchTime, maxBatchTime); }

        [Event(
            eventId: 8,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Saved {0} records")]
        public void SavedDownloadFacts(int count)
        { WriteEvent(8, count); }

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
            Message = "An error occurring replicating a batch. Batch Size: {0}. Source Max Original Key: {1}; Destination Cursor: {2}.")]
        public void BatchFailed(int batchSize, int sourceMaxKey, string destinationCursor)
        { WriteEvent(16, batchSize, sourceMaxKey, destinationCursor); }

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
            Message = "Aborting - Unable to process minimum batch size. Batch Size: {0}. Source Max Original Key: {1}; Destination Cursor: {2}")]
        public void UnableToProcessMinimumBatchSize(int batchSize, int sourceMaxKey, string destinationCursor)
        { WriteEvent(20, batchSize, sourceMaxKey, destinationCursor); }

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
            Message = "Getting replication source marker")]
        public void GettingSourceReplicationMarker()
        { WriteEvent(24); }

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
            Message = "Clearing existing records for the specified time window of {0} to {1}.")]
        public void ClearingDownloadFacts(DateTime minTime, DateTime maxTime)
        { WriteEvent(26, minTime, maxTime); }

        [Event(
            eventId: 27,
            Task = Tasks.ClearingDownloadFacts,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Cleared {0} existing records. {1}")]
        public void ClearedDownloadFacts(int factsCleared, string extraMessage)
        { WriteEvent(27, factsCleared, extraMessage); }

        [Event(
            eventId: 28,
            Task = Tasks.GettingTargetTimeWindowToProcess,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting the next target time window within the range of {0} to {1} where the source has records.")]
        public void GettingNextTargetTimeWindow(DateTime minTimestamp, DateTime maxTimestamp)
        { WriteEvent(28, minTimestamp, maxTimestamp); }

        [Event(
            eventId: 29,
            Task = Tasks.GettingTargetTimeWindowToProcess,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Got the next target time window. Time window to process is {0} to {1}. {2}.")]
        public void GotNextTargetTimeWindow(DateTime minTimestamp, DateTime maxTimestamp, string status)
        { WriteEvent(29, minTimestamp, maxTimestamp, status); }

        [Event(
            eventId: 30,
            Level = EventLevel.Informational,
            Message = "Verifying cursor for {0} to {1}. Expected cursor: {2}.")]
        public void VerifyingCursor(DateTime minTimestamp, DateTime maxTimestamp, string cursor)
        { WriteEvent(30, minTimestamp, maxTimestamp, cursor); }

        [Event(
            eventId: 31,
            Level = EventLevel.Informational,
            Message = "Verified cursor.")]
        public void VerifiedCursor()
        { WriteEvent(31); }

        [Event(
            eventId: 32,
            Level = EventLevel.Informational,
            Message = "An error occurred while saving a batch, but the batch was in fact committed. Recovering. Cursor min timestamp: {0}, max timestamp: {1}. Previous cursor value: {2}. New cursor value from the new batch: {3}. Committed cursor value: {4}. Exception that occurred: {5}")]
        public void RecoveredFromErrorSavingDownloadFacts(DateTime minTimestamp, DateTime maxTimestamp, string previousCursor, string newCursor, string committedCursor, string exceptionMessage)
        { WriteEvent(32, minTimestamp, maxTimestamp, previousCursor, newCursor, committedCursor, exceptionMessage); }

        [Event(
            eventId: 33,
            Level = EventLevel.Informational,
            Message = "Checking the committed cursor.")]
        public void CheckingCursor()
        { WriteEvent(33); }

        [Event(
            eventId: 34,
            Level = EventLevel.Informational,
            Message = "The committed cursor value is {0}.")]
        public void CheckedCursor(string committedCursor)
        { WriteEvent(34, committedCursor); }

        [Event(
            eventId: 35,
            Level = EventLevel.Informational,
            Message = "The batch failed, but we're going to recover. Current failure count: {0}. We'll give up after {1} failures in a row. Error: {2}")]
        public void RecoveredFromFailedBatch(int failureCount, int maxFailures, string error)
        { WriteEvent(35, failureCount, maxFailures, error); }

        [Event(
            eventId: 36,
            Level = EventLevel.Informational,
            Message = "There are no records to replicate. Finished.")]
        public void NothingToReplicate()
        { WriteEvent(36); }

        [Event(
            eventId: 37,
            Level = EventLevel.Warning,
            Message = "An error occurred during attempt {0} of {1}. {2}")]
        public void ErrorOccurred(int attempt, int maxFailures, string error)
        { WriteEvent(37, attempt, maxFailures, error); }

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
