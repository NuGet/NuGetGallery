using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dapper;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    [Description("Creates copies of Package Blobs based on information in the NuGet API v2 Database.")]
    public class BackupPackageBlobsJob : JobHandler<BackupPackageBlobsEventSource>
    {
        public static readonly string DefaultSourceContainer = "packages";
        public static readonly string DefaultDestinationContainer = "ng-backups";
        public static readonly string BackupStateBlobName = "__backupstate";

        private const string SourceBlobFormat = "{0}.{1}.nupkg";
        private const string DestinationBlobFormat = "packages/{0}/{1}/{2}.nupkg";

        // AzCopy uses this, so it seems good.
        private const int TaskPerCoreFactor = 8;

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public AzureStorageReference Source { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the destination
        /// </summary>
        public AzureStorageReference Destination { get; set; }

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
            // Load default data if not provided
            PackageDatabase = PackageDatabase ?? Config.Sql.GetConnectionString(KnownSqlServer.Primary);
            var sourceAccount = Source == null ?
                Storage.Legacy :
                Storage.GetAccount(Source);
            SourceContainer = sourceAccount.Blobs.Client.GetContainerReference(
                Source == null ? DefaultSourceContainer : Source.Container);
            var destAccount = Destination == null ?
                Storage.Backup :
                Storage.GetAccount(Destination);
            DestinationContainer = destAccount.Blobs.Client.GetContainerReference(
                Destination == null ? DefaultDestinationContainer : Destination.Container);
            Log.PreparingToBackup(sourceAccount.Name, SourceContainer.Name, destAccount.Name, DestinationContainer.Name, PackageDatabase.DataSource, PackageDatabase.InitialCatalog);

            // Load package state if we aren't doing a full rescan
            Log.LoadingBackupState(destAccount.Name, DestinationContainer.Name);
            var lastBackup = FullRescan ? 
                (DateTimeOffset?)null :
                await LoadBackupState(destAccount, DestinationContainer);
            Log.LoadedBackupState(destAccount.Name, DestinationContainer.Name);

            // Gather packages
            Log.GatheringListOfPackages(PackageDatabase.DataSource, PackageDatabase.InitialCatalog, lastBackup);
            IList<PackageReference> packages;
            using(var connection = await PackageDatabase.ConnectTo()) {
                packages = (await connection.QueryAsync<PackageReference>(@"
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

            // Start a dataflow to parallelize the transfer
            var action = new TransformBlock<PackageReference, CloudBlockBlob>(
                package => BackupPackage(package),
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount * TaskPerCoreFactor
                });
            
            // Create an action block that is single-threaded to receive completed backups for a heartbeat
            var completed = new ActionBlock<CloudBlockBlob>(blob => BackupCompleted(blob));
            action.LinkTo(completed);

            Log.StartingBackup(packages.Count);
            foreach (var package in packages)
            {
                action.Post(package);
            }
            action.Complete();
            
            await completed.Completion;
            Log.StartedBackup();
        }

        private async Task BackupCompleted(CloudBlockBlob backupBlob)
        {
            if (Invocation.NextVisibleAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(1))
            {
                // Running out of time! Extend the job
                Log.ExtendingJobLeaseWhileBackupProgresses();
                await Extend(TimeSpan.FromMinutes(5));
                Log.ExtendedJobLease();
            }
            // TODO: Can we find a way to monitor the copies? It could be a lot of copies...
        }

        private async Task<CloudBlockBlob> BackupPackage(PackageReference package)
        {
            // Identify the source and destination blobs
            var sourceBlob = SourceContainer.GetBlockBlobReference(
                String.Format(
                    CultureInfo.InvariantCulture, 
                    SourceBlobFormat, 
                    package.Id, 
                    package.Version).ToLowerInvariant());
            var destBlob = DestinationContainer.GetBlockBlobReference(
                String.Format(
                    CultureInfo.InvariantCulture,
                    DestinationBlobFormat,
                    package.Id,
                    package.Version,
                    package.Hash).ToLowerInvariant());

            if (await destBlob.ExistsAsync())
            {
                Log.BackupExists(destBlob.Name);
                return null; // Package does not need waiting
            }
            else if(!await sourceBlob.ExistsAsync())
            {
                Log.SourceBlobMissing(sourceBlob.Name);
                return null;
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
                return destBlob;
            }
        }

        private async Task<DateTimeOffset?> LoadBackupState(StorageAccountHub account, CloudBlobContainer container)
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
        public void GatheredListOfPackages(int gathered, string dbServer, string dbName) { WriteEvent(5, dbServer, dbName); }

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

        public static class Tasks {
            public const EventTask LoadingBackupState = (EventTask)0x1;
            public const EventTask GatheringPackages = (EventTask)0x2;
            public const EventTask BackingUpPackages = (EventTask)0x3;
            public const EventTask StartingPackageCopy = (EventTask)0x4;
            public const EventTask ExtendingJobLease = (EventTask)0x5;
        }
    }
}
