using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    [Description("Creates copies of Package Blobs based on information in the NuGet API v2 Database.")]
    public class BackupPackageBlobsJob : JobHandler<BackupPackageBlobsEventSource>
    {
        public static readonly string BackupStateBlobName = "__backupstate";


        // AzCopy uses this, so it seems good.
        private const int TaskPerCoreFactor = 8;

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public CloudStorageAccount Source { get; set; }
        public string SourceContainerName { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the destination
        /// </summary>
        public CloudStorageAccount Destination { get; set; }
        public string DestinationContainerName { get; set; }

        /// <summary>
        /// Gets or sets a connection string to the database containing package data.
        /// </summary>
        public SqlConnectionStringBuilder PackageDatabase { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating if the job should do a full rescan of package backups.
        /// </summary>
        public bool FullRescan { get; set; }

        protected ConfigurationHub Config { get; private set; }
        protected StorageHub Storage { get; private set; }

        protected CloudBlobContainer SourceContainer { get; private set; }
        protected CloudBlobContainer DestinationContainer { get; private set; }

        public BackupPackageBlobsJob(ConfigurationHub config, StorageHub storage)
        {
            Config = config;
            Storage = storage;
        }

        protected internal override async Task Execute()
        {
            var now = DateTimeOffset.UtcNow;

            // Load default data if not provided
            PackageDatabase = PackageDatabase ?? Config.Sql.GetConnectionString(KnownSqlServer.Legacy);
            Source = Source ?? Config.Storage.Legacy;
            Destination = Destination ?? Config.Storage.Backup;
            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(
                String.IsNullOrEmpty(SourceContainerName) ? BlobContainerNames.LegacyPackages : SourceContainerName);
            DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(
                String.IsNullOrEmpty(DestinationContainerName) ? BlobContainerNames.Backups : DestinationContainerName);
            Log.PreparingToBackup(Source.Credentials.AccountName, SourceContainer.Name, Destination.Credentials.AccountName, DestinationContainer.Name, PackageDatabase.DataSource, PackageDatabase.InitialCatalog);

            // Load package state if we aren't doing a full rescan
            Log.LoadingBackupState(Destination.Credentials.AccountName, DestinationContainer.Name);
            var lastBackup = FullRescan ? 
                (DateTimeOffset?)null :
                await LoadBackupState(DestinationContainer);
            Log.LoadedBackupState(Destination.Credentials.AccountName, DestinationContainer.Name);

            // Gather packages
            Log.GatheringListOfPackages(PackageDatabase.DataSource, PackageDatabase.InitialCatalog, lastBackup);
            IList<PackageRef> packages;
            using(var connection = await PackageDatabase.ConnectTo()) {
                packages = (await connection.QueryAsync<PackageRef>(@"
                    SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash
                    FROM Packages p
                    INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
                    WHERE @lastBackup IS NULL AND [NormalizedVersion] IS NOT NULL
                    OR (
                        (p.[LastEdited] IS NULL AND p.[Published] > @lastBackup) OR
                        (p.[LastEdited] IS NOT NULL AND p.[LastEdited] > @lastBackup)
                    )", new { lastBackup })).ToList();
            }
            Log.GatheredListOfPackages(packages.Count, PackageDatabase.DataSource, PackageDatabase.InitialCatalog);

            if (!WhatIf)
            {
                await DestinationContainer.CreateIfNotExistsAsync();
            }

            var action = new TransformBlock<PackageRef, object>(package =>
            {
                InvocationContext.SetCurrentInvocationId(Invocation.Id);
                return BackupPackage(package);
            }, new ExecutionDataflowBlockOptions() {
                CancellationToken = Context.CancelToken,
                MaxDegreeOfParallelism = TaskPerCoreFactor * Environment.ProcessorCount,
                MaxMessagesPerTask = 1,
            });
            var extendIfNecessary = new ActionBlock<object>(async blob =>
            {
                InvocationContext.SetCurrentInvocationId(Invocation.Id);
                if ((Invocation.NextVisibleAt - DateTimeOffset.UtcNow) < TimeSpan.FromMinutes(1))
                {
                    // Running out of time! Extend the job
                    Log.ExtendingJobLeaseWhileBackupProgresses();
                    await Extend(TimeSpan.FromMinutes(5));
                    Log.ExtendedJobLease();
                }
                else
                {
                    Log.JobLeaseOk();
                }
            }, new ExecutionDataflowBlockOptions()
            {
                CancellationToken = Context.CancelToken,
                MaxDegreeOfParallelism = 1,
            });
            action.LinkTo(extendIfNecessary);
            
            Log.StartingBackup(packages.Count);
            foreach (var package in packages)
            {
                action.Post(package);
            }
            action.Complete();
            Log.StartedBackup();
            await extendIfNecessary.Completion;

            // Write backup state
            if (!Context.CancelToken.IsCancellationRequested)
            {
                Log.SavingBackupState(Destination.Credentials.AccountName, DestinationContainer.Name);
                await WriteBackupState(DestinationContainer, now);
                Log.SavedBackupState(Destination.Credentials.AccountName, DestinationContainer.Name);
            }
        }

        private static readonly object Unit = new object();
        private async Task<object> BackupPackage(PackageRef package)
        {
            // Identify the source and destination blobs
            var sourceBlob = SourceContainer.GetBlockBlobReference(
                PackageHelpers.GetPackageBlobName(package));
            var destBlob = DestinationContainer.GetBlockBlobReference(
                PackageHelpers.GetPackageBackupBlobName(package));

            if (await destBlob.ExistsAsync())
            {
                Log.BackupExists(destBlob.Name);
                return Unit;
            }
            else if (!await sourceBlob.ExistsAsync())
            {
                Log.SourceBlobMissing(sourceBlob.Name);
                return Unit;
            }
            else
            {
                // Start the copy
                Log.StartingCopy(sourceBlob.Name, destBlob.Name);
                if (!WhatIf)
                {
                    await destBlob.StartCopyFromBlobAsync(sourceBlob);
                }
                Log.StartedCopy(sourceBlob.Name, destBlob.Name);
                return Unit;
            }
        }

        private async Task WriteBackupState(CloudBlobContainer container, DateTimeOffset now)
        {
            if (!WhatIf)
            {
                await container.CreateIfNotExistsAsync();
                var blob = container.GetBlockBlobReference(BackupStateBlobName);
                await blob.UploadTextAsync(now.ToString("O"));
            }
        }

        private async Task<DateTimeOffset?> LoadBackupState(CloudBlobContainer container)
        {
            if (!await container.ExistsAsync())
            {
                return null;
            }
            var blob = container.GetBlockBlobReference(BackupStateBlobName);
            if (!await blob.ExistsAsync())
            {
                return null;
            }
            await blob.FetchAttributesAsync();
            return blob.Properties.LastModified;
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-BackupPackageBlobs")]
    public class BackupPackageBlobsEventSource : EventSource
    {
        public static readonly BackupPackageBlobsEventSource Log = new BackupPackageBlobsEventSource();
        private BackupPackageBlobsEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to backup package blobs from {0}/{1} to {2}/{3} using package data from {4}/{5}")]
        public void PreparingToBackup(string sourceAccount, string sourceContainer, string destAccount, string destContainer, string dbServer, string dbName) { WriteEvent(1, sourceAccount, sourceContainer, destAccount, destContainer, dbServer, dbName); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Task = Tasks.LoadingBackupState,
            Opcode = EventOpcode.Start,
            Message = "Loading Backup state from {0}/{1}")]
        public void LoadingBackupState(string account, string container) { WriteEvent(2, account, container); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Task = Tasks.LoadingBackupState,
            Opcode = EventOpcode.Stop,
            Message = "Loaded Backup state from {0}/{1}")]
        public void LoadedBackupState(string account, string container) { WriteEvent(3, account, container); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.GatheringPackages,
            Opcode = EventOpcode.Start,
            Message = "Gathering list of packages from {0}/{1} published or edited since {2}")]
        private void GatheringListOfPackages(string dbServer, string dbName, string modifiedSince) { WriteEvent(4, dbServer, dbName, modifiedSince); }
        [NonEvent]
        public void GatheringListOfPackages(string dbServer, string dbName, DateTimeOffset? modifiedSince) { GatheringListOfPackages(dbServer, dbName, modifiedSince == null ? "the beginning of time" : modifiedSince.Value.ToString("O")); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.GatheringPackages,
            Opcode = EventOpcode.Stop,
            Message = "Gathered {0} packages from {1}/{2}")]
        public void GatheredListOfPackages(int gathered, string dbServer, string dbName) { WriteEvent(5, gathered, dbServer, dbName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = Tasks.ExtendingJobLease,
            Opcode = EventOpcode.Start,
            Message = "Extending job lease while backup progresses")]
        public void ExtendingJobLeaseWhileBackupProgresses() { WriteEvent(6); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.ExtendingJobLease,
            Opcode = EventOpcode.Stop,
            Message = "Extended job lease while backup progresses")]
        public void ExtendedJobLease() { WriteEvent(7); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Message = "Backup already exists: {0}")]
        public void BackupExists(string blobName) { WriteEvent(8, blobName); }

        [Event(
            eventId: 9,
            Level = EventLevel.Error,
            Message = "Source Blob does not exist: {0}")]
        public void SourceBlobMissing(string blobName) { WriteEvent(9, blobName); }

        [Event(
            eventId: 10,
            Level = EventLevel.Informational,
            Task = Tasks.BackingUpPackages,
            Opcode = EventOpcode.Start,
            Message = "Starting backup of {0} packages.")]
        public void StartingBackup(int count) { WriteEvent(10, count); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.BackingUpPackages,
            Opcode = EventOpcode.Stop,
            Message = "Started backups.")]
        public void StartedBackup() { WriteEvent(11); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.StartingPackageCopy,
            Opcode = EventOpcode.Start,
            Message = "Starting copy of {0} to {1}.")]
        public void StartingCopy(string source, string dest) { WriteEvent(12, source, dest); }

        [Event(
            eventId: 13,
            Level = EventLevel.Informational,
            Task = Tasks.StartingPackageCopy,
            Opcode = EventOpcode.Stop,
            Message = "Started copy of {0} to {1}.")]
        public void StartedCopy(string source, string dest) { WriteEvent(13, source, dest); }

        [Event(
            eventId: 14,
            Level = EventLevel.Informational,
            Task = Tasks.SavingBackupState,
            Opcode = EventOpcode.Start,
            Message = "Saving backup stage to {0}/{1}")]
        public void SavingBackupState(string destAccount, string destContainer) { WriteEvent(14, destAccount, destContainer); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Task = Tasks.SavingBackupState,
            Opcode = EventOpcode.Stop,
            Message = "Saved backup stage to {0}/{1}")]
        public void SavedBackupState(string destAccount, string destContainer) { WriteEvent(15, destAccount, destContainer); }

        [Event(
            eventId: 16,
            Level = EventLevel.Informational,
            Message = "Job lease OK")]
        public void JobLeaseOk() { WriteEvent(16); }

        public static class Tasks
        {
            public const EventTask LoadingBackupState = (EventTask)0x1;
            public const EventTask GatheringPackages = (EventTask)0x2;
            public const EventTask BackingUpPackages = (EventTask)0x3;
            public const EventTask StartingPackageCopy = (EventTask)0x4;
            public const EventTask ExtendingJobLease = (EventTask)0x5;
            public const EventTask SavingBackupState = (EventTask)0x6;
        }
    }
}
