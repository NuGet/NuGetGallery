using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NuGet.Services.Configuration;

namespace NuGet.Services.Work.Jobs
{
    [Description("Replicates package statistics from the primary database to the warehouse")]
    public class ReplicatePackageStatisticsJob : JobHandler<ReplicatePackageStatisticsEventSource>
    {
        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder Source { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing warehouse data.
        /// </summary>
        public SqlConnectionStringBuilder Destination { get; set; }

        protected ConfigurationHub Config { get; private set; }

        public ReplicatePackageStatisticsJob(ConfigurationHub config)
        {
            Config = config;
        }

        protected internal override async Task Execute()
        {
            // Load defaults
            Source = Source ?? Config.Sql.Legacy;
            Destination = Destination ?? Config.Sql.Warehouse;

            Log.ReplicatingStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog);

            const int BatchSize = 1000;                 //  number of rows to collect from the source
            const int ExpectedSourceMaxQueryTime = 5;   //  if the query from the source database takes longer than this we must be busy
            const int PauseDuration = 10;               //  pause applied when the queries to the source are taking a long time 

            var count = await Replicate(BatchSize, ExpectedSourceMaxQueryTime, PauseDuration);
            Log.ReplicatedStatistics(Source.DataSource, Source.InitialCatalog, Destination.DataSource, Destination.InitialCatalog, count);
        }

        public static async Task<int> GetLastOriginalKey(SqlConnectionStringBuilder connectionString)
        {
            using (var connection = await connectionString.ConnectTo())
            {
                SqlCommand command = new SqlCommand("GetLastOriginalKey", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 120;

                SqlParameter resultParam = command.CreateParameter();
                resultParam.Direction = ParameterDirection.Output;
                resultParam.DbType = DbType.Int32;
                resultParam.ParameterName = "@OriginalKey";

                command.Parameters.Add(resultParam);

                await command.ExecuteNonQueryAsync();

                if (resultParam.Value is DBNull)
                {
                    return 0;
                }

                return (int)resultParam.Value;
            }
        }

        private async Task<List<DownloadFact>> GetDownloadRecords(int originalKey, int top)
        {
            using (var connection = await Source.ConnectTo())
            {
                return (await connection.QueryAsync<DownloadFact>(@"
                    SELECT TOP(@top) 
                        PackageStatistics.[Key] 'OriginalKey', 
                        PackageRegistrations.[Id] 'PackageId', 
                        Packages.[Version] 'PackageVersion', 
	                    Packages.[Listed] 'PackageListed',
                        Packages.[Title] 'PackageTitle',
                        Packages.[Description] 'PackageDescription',
                        Packages.[IconUrl] 'PackageIconUrl',
                        ISNULL(PackageStatistics.[UserAgent], '') 'DownloadUserAgent', 
                        ISNULL(PackageStatistics.[Operation], '') 'DownloadOperation', 
                        PackageStatistics.[Timestamp] 'DownloadTimestamp',
                        PackageStatistics.[ProjectGuids] 'DownloadProjectTypes',
                        PackageStatistics.[DependentPackage] 'DownloadDependentPackageId'
                    FROM PackageStatistics 
                    INNER JOIN Packages ON PackageStatistics.PackageKey = Packages.[Key] 
                    INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.PackageRegistrationKey 
                    WHERE PackageStatistics.[Key] > @originalKey 
                    ", new
                    {
                        originalKey,
                        top
                    })).ToList();
            }
        }

        private async Task PutDownloadRecords(List<DownloadFact> batch)
        {
            using (var connection = await Destination.ConnectTo())
            {
                foreach (DownloadFact fact in batch)
                {
                    await connection.QueryAsync<int>(
                        "AddDownloadFact",
                        param: new
                        {
                            fact.OriginalKey,
                            fact.PackageId,
                            fact.PackageVersion,
                            fact.PackageListed,
                            fact.PackageTitle,
                            fact.PackageDescription,
                            fact.PackageIconUrl,
                            fact.DownloadUserAgent,
                            fact.DownloadOperation,
                            fact.DownloadTimestamp,
                            fact.DownloadProjectTypes,
                            fact.DownloadDependentPackageId
                        },
                        commandType: CommandType.StoredProcedure);
                }
            }
        }

        private async Task<int> Replicate(int batchSize, int expectedSourceMaxQueryTime, int pauseDuration)
        {
            int total = 0;

            bool hasWork;
            int lastKey = -1;
            do
            {
                Log.GettingLastReplicatedKey(Destination.DataSource, Destination.InitialCatalog);
                var originalKey = await GetLastOriginalKey(Destination);
                Log.GotLastReplicatedKey(Destination.DataSource, Destination.InitialCatalog, originalKey);
                if (lastKey != -1 && lastKey == originalKey)
                {
                    Log.LastReplicatedKeyNotChanged();
                    return total;
                }
                lastKey = originalKey;

                Log.FetchingStatisticsChunk(Source.DataSource, Source.InitialCatalog, batchSize);
                var batch = await GetDownloadRecords(originalKey, batchSize);
                Log.FetchedStatisticsChunk(Source.DataSource, Source.InitialCatalog, batch.Count);
                
                if (batch.Count > 0)
                {
                    hasWork = true;
                    Log.SavingDownloadFacts(Destination.InitialCatalog, Destination.DataSource, batch.Count);
                    if (!WhatIf)
                    {
                        await PutDownloadRecords(batch);
                    }
                    Log.SavedDownloadFacts(Destination.InitialCatalog, Destination.DataSource, batch.Count);

                    total += batch.Count;
                }
                else
                {
                    hasWork = false;
                }
            }
            while (hasWork);

            return total;
        }

        private class DownloadFact
        {
            public int OriginalKey { get; private set; }
            public string PackageId { get; private set; }
            public string PackageVersion { get; private set; }
            public bool PackageListed { get; private set; }
            public string PackageTitle { get; private set; }
            public string PackageDescription { get; private set; }
            public string PackageIconUrl { get; private set; }
            public string DownloadUserAgent { get; private set; }
            public string DownloadOperation { get; private set; }
            public DateTime DownloadTimestamp { get; private set; }
            public string DownloadProjectTypes { get; private set; }
            public string DownloadDependentPackageId { get; private set; }

            // Project Types is defined to be a semicolon set of identifiers, the identifiers are typically GUIDs
            // The Project Types data should be treated as a set where the order of the fields does not matter for equality
            // So we normalize the Project Types data so we can use string comparison for equality in the warehouse queries

            private static string NormalizeProjectTypes(string original)
            {
                if (string.IsNullOrEmpty(original))
                {
                    return original;
                }

                string[] fields = original.ToLowerInvariant().Split(';');

                Array.Sort(fields);

                StringBuilder sb = new StringBuilder();
                int i = 0;
                for (; i < (fields.Length - 1); i++)
                {
                    sb.Append(fields[i]);
                    sb.Append(';');
                }
                sb.Append(fields[i]);

                string normalized = sb.ToString();

                //  not strictly necessary but GUIDs are the norm and people expect to read GUID values in uppercase
                return normalized.ToUpperInvariant();
            }

            private static string GetNullableField(SqlDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal))
                {
                    return null;
                }
                return reader.GetString(ordinal);
            }
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-ReplicatePackageStatistics")]
    public class ReplicatePackageStatisticsEventSource : EventSource
    {
        public static readonly ReplicatePackageStatisticsEventSource Log = new ReplicatePackageStatisticsEventSource();
        private ReplicatePackageStatisticsEventSource() { }

        [Event(
            eventId: 1,
            Task = Tasks.ReplicatingStatistics,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Replicating statistics from {0}/{1} to {2}/{3}")]
        public void ReplicatingStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase) { WriteEvent(1, sourceServer, sourceDatabase, destServer, destDatabase); }

        [Event(
            eventId: 2,
            Task = Tasks.ReplicatingStatistics,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Replicated {4} download facts from {0}/{1} to {2}/{3}")]
        public void ReplicatedStatistics(string sourceServer, string sourceDatabase, string destServer, string destDatabase, int count) { WriteEvent(2, sourceServer, sourceDatabase, destServer, destDatabase, count); }

        [Event(
            eventId: 3,
            Task = Tasks.GettingLastReplicatedKey,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting last replicated key from {0}/{1}")]
        public void GettingLastReplicatedKey(string server, string database) { WriteEvent(3, server, database); }

        [Event(
            eventId: 4,
            Task = Tasks.GettingLastReplicatedKey,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Last replicated key from {0}/{1} is {2}")]
        public void GotLastReplicatedKey(string server, string database, int key) { WriteEvent(4, server, database, key); }

        [Event(
            eventId: 5,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Fetching {2} statistics entries from {0}/{1}")]
        public void FetchingStatisticsChunk(string server, string database, int limit) { WriteEvent(5, server, database, limit); }

        [Event(
            eventId: 6,
            Task = Tasks.FetchingStatisticsChunk,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Fetched {2} statistics entries from {0}/{1}")]
        public void FetchedStatisticsChunk(string server, string database, int count) { WriteEvent(6, server, database, count); }

        [Event(
            eventId: 7,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Saving {2} download facts to {0}/{1}")]
        public void SavingDownloadFacts(string server, string database, int count) { WriteEvent(7, server, database, count); }

        [Event(
            eventId: 8,
            Task = Tasks.SavingDownloadFacts,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Saved {2} download facts to {0}/{1}")]
        public void SavedDownloadFacts(string server, string database, int count) { WriteEvent(8, server, database, count); }

        [Event(
            eventId: 9,
            Task = Tasks.GettingLastReplicatedKey,
            Level = EventLevel.Warning,
            Message = "Last replicated key has not changed meaning no data was inserted last run. Stopping")]
        public void LastReplicatedKeyNotChanged() { WriteEvent(9); }

        public static class Tasks
        {
            public const EventTask ReplicatingStatistics = (EventTask)0x1;
            public const EventTask GettingLastReplicatedKey = (EventTask)0x2;
            public const EventTask FetchingStatisticsChunk = (EventTask)0x3;
            public const EventTask SavingDownloadFacts = (EventTask)0x4;
        }
    }
}
