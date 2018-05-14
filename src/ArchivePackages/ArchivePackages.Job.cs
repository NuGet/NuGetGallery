// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;

namespace ArchivePackages
{
    public class Job : JobBase
    {
        private readonly JobEventSource JobEventSourceLog = JobEventSource.Log;
        private const string ContentTypeJson = "application/json";
        private const string DateTimeFormatSpecifier = "O";
        private const string CursorDateTimeKey = "cursorDateTime";
        private const string DefaultPackagesContainerName = "packages";
        private const string DefaultPackagesArchiveContainerName = "ng-backups";
        private const string DefaultCursorBlobName = "cursor.json";

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the source for package blobs
        /// </summary>
        public CloudStorageAccount Source { get; set; }

        public string SourceContainerName { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the destination
        /// </summary>
        public CloudStorageAccount PrimaryDestination { get; set; }

        /// <summary>
        /// Gets or sets an Azure Storage Uri referring to a container to use as the secondary destination
        /// DestinationContainerName should be same as the primary destination
        /// </summary>
        public CloudStorageAccount SecondaryDestination { get; set; }
        /// <summary>
        /// Destination Container name for both Primary and Secondary destinations. Also, for the cursor blob
        /// </summary>
        public string DestinationContainerName { get; set; }

        /// <summary>
        /// Blob containing the cursor data. Cursor data comprises of cursorDateTime
        /// </summary>
        public string CursorBlobName { get; set; }
        
        private ISqlConnectionFactory _packageDbConnectionFactory;

        protected CloudBlobContainer SourceContainer { get; private set; }

        protected CloudBlobContainer PrimaryDestinationContainer { get; private set; }

        protected CloudBlobContainer SecondaryDestinationContainer { get; private set; }

        public Job() : base(JobEventSource.Log) { }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));
            var packageDbConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase);
            _packageDbConnectionFactory = new AzureSqlConnectionFactory(packageDbConnectionString, secretInjector);

            Source = CloudStorageAccount.Parse(
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.Source));

            PrimaryDestination = CloudStorageAccount.Parse(
                                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination));

            var secondaryDestinationCstr = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.SecondaryDestination);
            SecondaryDestination = string.IsNullOrEmpty(secondaryDestinationCstr) ? null : CloudStorageAccount.Parse(secondaryDestinationCstr);

            SourceContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.SourceContainerName) ?? DefaultPackagesContainerName;

            DestinationContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultPackagesArchiveContainerName;

            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(SourceContainerName);
            PrimaryDestinationContainer = PrimaryDestination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            SecondaryDestinationContainer = SecondaryDestination?.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

            CursorBlobName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.CursorBlob) ?? DefaultCursorBlobName;
        }

        public override async Task Run()
        {
            JobEventSourceLog.PreparingToArchive(Source.Credentials.AccountName, SourceContainer.Name, PrimaryDestination.Credentials.AccountName, PrimaryDestinationContainer.Name, _packageDbConnectionFactory.DataSource, _packageDbConnectionFactory.InitialCatalog);
            await Archive(PrimaryDestinationContainer);

            // todo: consider reusing package query for primary and secondary archives
            if (SecondaryDestinationContainer != null)
            {
                JobEventSourceLog.PreparingToArchive2(SecondaryDestination.Credentials.AccountName, SecondaryDestinationContainer.Name);
                await Archive(SecondaryDestinationContainer);
            }
        }

        private static async Task<JObject> GetJObject(CloudBlobContainer container, string blobName)
        {
            var blob = container.GetBlockBlobReference(blobName);
            var json = await blob.DownloadTextAsync();
            return JObject.Parse(json);
        }

        private static async Task SetJObject(CloudBlobContainer container, string blobName, JObject jObject)
        {
            var blob = container.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = ContentTypeJson;
            await blob.UploadTextAsync(jObject.ToString());
        }

        private async Task Archive(CloudBlobContainer destinationContainer)
        {
            var cursorJObject = await GetJObject(destinationContainer, CursorBlobName);
            var cursorDateTime = cursorJObject[CursorDateTimeKey].Value<DateTime>();

            JobEventSourceLog.CursorData(cursorDateTime.ToString(DateTimeFormatSpecifier));

            JobEventSourceLog.GatheringPackagesToArchiveFromDb(_packageDbConnectionFactory.DataSource, _packageDbConnectionFactory.InitialCatalog);
            List<PackageRef> packages;
            using (var connection = await _packageDbConnectionFactory.CreateAsync())
            {
                packages = (await connection.QueryAsync<PackageRef>(@"
			    SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, p.LastEdited, p.Published
			    FROM Packages p
			    INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
			    WHERE Published > @cursorDateTime OR LastEdited > @cursorDateTime", new { cursorDateTime = cursorDateTime }))
                    .ToList();
            }
            JobEventSourceLog.GatheredPackagesToArchiveFromDb(packages.Count, _packageDbConnectionFactory.DataSource, _packageDbConnectionFactory.InitialCatalog);

            var archiveSet = packages
                .AsParallel()
                .Select(r => Tuple.Create(StorageHelpers.GetPackageBlobName(r.Id, r.Version), StorageHelpers.GetPackageBackupBlobName(r.Id, r.Version, r.Hash)))
                .ToList();

            //if (!WhatIf)
            {
                await destinationContainer.CreateIfNotExistsAsync();
            }

            if (archiveSet.Count > 0)
            {
                JobEventSourceLog.StartingArchive(archiveSet.Count);
                foreach (var archiveItem in archiveSet)
                {
                    await ArchivePackage(archiveItem.Item1, archiveItem.Item2, SourceContainer, destinationContainer);
                }

                var maxLastEdited = packages.Max(p => p.LastEdited);
                var maxPublished = packages.Max(p => p.Published);

                // Time is ever increasing after all, simply store the max of published and lastEdited as cursorDateTime
                var newCursorDateTime = maxLastEdited > maxPublished ? new DateTime(maxLastEdited.Value.Ticks, DateTimeKind.Utc) : new DateTime(maxPublished.Value.Ticks, DateTimeKind.Utc);
                var newCursorDateTimeString = newCursorDateTime.ToString(DateTimeFormatSpecifier);

                JobEventSourceLog.NewCursorData(newCursorDateTimeString);
                cursorJObject[CursorDateTimeKey] = newCursorDateTimeString;
                await SetJObject(destinationContainer, CursorBlobName, cursorJObject);
            }
        }

        private async Task ArchivePackage(string sourceBlobName, string destinationBlobName, CloudBlobContainer sourceContainer, CloudBlobContainer destinationContainer)
        {
            // Identify the source and destination blobs
            var sourceBlob = sourceContainer.GetBlockBlobReference(sourceBlobName);
            var destBlob = destinationContainer.GetBlockBlobReference(destinationBlobName);

            if (await destBlob.ExistsAsync())
            {
                JobEventSourceLog.ArchiveExists(destBlob.Name);
            }
            else if (!await sourceBlob.ExistsAsync())
            {
                JobEventSourceLog.SourceBlobMissing(sourceBlob.Name);
            }
            else
            {
                // Start the copy
                JobEventSourceLog.StartingCopy(sourceBlob.Name, destBlob.Name);
                //if (!WhatIf)
                {
                    await destBlob.StartCopyAsync(sourceBlob);
                }
                JobEventSourceLog.StartedCopy(sourceBlob.Name, destBlob.Name);
            }
        }
    }

    [EventSource(Name = "Outercurve-NuGet-Jobs-ArchivePackages")]
    public class JobEventSource : EventSource
    {
        public static readonly JobEventSource Log = new JobEventSource();

        private JobEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to archive packages from {0}/{1} to primary destination {2}/{3} using package data from {4}/{5}")]
        public void PreparingToArchive(string sourceAccount, string sourceContainer, string destAccount, string destContainer, string dbServer, string dbName) { WriteEvent(1, sourceAccount, sourceContainer, destAccount, destContainer, dbServer, dbName); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Message = "Preparing to archive packages to secondary destination {0}/{1}")]
        public void PreparingToArchive2(string destAccount, string destContainer) { WriteEvent(2, destAccount, destContainer); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Message = "Cursor data: CursorDateTime is {0}")]
        public void CursorData(string cursorDateTime) { WriteEvent(3, cursorDateTime); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.GatheringDbPackages,
            Opcode = EventOpcode.Start,
            Message = "Gathering list of packages to archive from {0}/{1}")]
        public void GatheringPackagesToArchiveFromDb(string dbServer, string dbName) { WriteEvent(4, dbServer, dbName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.GatheringDbPackages,
            Opcode = EventOpcode.Stop,
            Message = "Gathered {0} packages to archive from {1}/{2}")]
        public void GatheredPackagesToArchiveFromDb(int gathered, string dbServer, string dbName) { WriteEvent(5, gathered, dbServer, dbName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = Tasks.ArchivingPackages,
            Opcode = EventOpcode.Start,
            Message = "Starting archive of {0} packages.")]
        public void StartingArchive(int count) { WriteEvent(6, count); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.ArchivingPackages,
            Opcode = EventOpcode.Stop,
            Message = "Started archive.")]
        public void StartedArchive() { WriteEvent(7); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Message = "Archive already exists: {0}")]
        public void ArchiveExists(string blobName) { WriteEvent(8, blobName); }

        [Event(
            eventId: 9,
            Level = EventLevel.Warning,
            Message = "Source Blob does not exist: {0}")]
        public void SourceBlobMissing(string blobName) { WriteEvent(9, blobName); }

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
            Message = "NewCursor data: CursorDateTime is {0}")]
        public void NewCursorData(string cursorDateTime) { WriteEvent(14, cursorDateTime); }
    }

    public static class Tasks
    {
        public const EventTask GatheringDbPackages = (EventTask)0x1;
        public const EventTask ArchivingPackages = (EventTask)0x2;
        public const EventTask StartingPackageCopy = (EventTask)0x3;
    }

    public class PackageRef
    {
        public PackageRef(string id, string version, string hash)
        {
            Id = id;
            Version = version;
            Hash = hash;
        }
        public PackageRef(string id, string version, string hash, DateTime lastEdited)
            : this(id, version, hash)
        {
            LastEdited = lastEdited;
        }
        public PackageRef(string id, string version, string hash, DateTime lastEdited, DateTime published)
            : this(id, version, hash, lastEdited)
        {
            Published = published;
        }
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }
        public DateTime? LastEdited { get; set; }
        public DateTime? Published { get; set; }
    }
}
