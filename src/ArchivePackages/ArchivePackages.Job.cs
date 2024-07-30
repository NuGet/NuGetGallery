// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace ArchivePackages
{
    public class Job : JsonConfigurationJob
    {
        private readonly JobEventSource JobEventSourceLog = JobEventSource.Log;
        private const string ContentTypeJson = "application/json";
        private const string DateTimeFormatSpecifier = "O";
        private const string CursorDateTimeKey = "cursorDateTime";
        private const string DefaultPackagesContainerName = "packages";
        private const string DefaultPackagesArchiveContainerName = "ng-backups";
        private const string DefaultCursorBlobName = "cursor.json";

        private InitializationConfiguration Configuration { get; set; }

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

        /// <summary>
        /// Gallery database registration, for diagnostics.
        /// </summary>
        private SqlConnectionStringBuilder GalleryDatabase { get; set; }

        protected CloudBlobContainer SourceContainer { get; private set; }

        protected CloudBlobContainer PrimaryDestinationContainer { get; private set; }

        protected CloudBlobContainer SecondaryDestinationContainer { get; private set; }

        public Job() : base(JobEventSource.Log) { }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            Configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;

            GalleryDatabase = GetDatabaseRegistration<GalleryDbConfiguration>();

            Source = CloudStorageAccount.Parse(Configuration.Source);

            PrimaryDestination = CloudStorageAccount.Parse(Configuration.PrimaryDestination);

            if (!string.IsNullOrEmpty(Configuration.SecondaryDestination))
            {
                SecondaryDestination = CloudStorageAccount.Parse(Configuration.SecondaryDestination);
            }

            SourceContainerName = Configuration.SourceContainerName ?? DefaultPackagesContainerName;
            DestinationContainerName = Configuration.DestinationContainerName ?? DefaultPackagesArchiveContainerName;

            SourceContainer = Source.CreateCloudBlobClient().GetContainerReference(SourceContainerName);
            PrimaryDestinationContainer = PrimaryDestination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            SecondaryDestinationContainer = SecondaryDestination?.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

            CursorBlobName = Configuration.CursorBlob ?? DefaultCursorBlobName;
        }

        public override async Task Run()
        {
            JobEventSourceLog.PreparingToArchive(Source.Credentials.AccountName, SourceContainer.Name, PrimaryDestination.Credentials.AccountName, PrimaryDestinationContainer.Name, GalleryDatabase.DataSource, GalleryDatabase.InitialCatalog);
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

            JobEventSourceLog.GatheringPackagesToArchiveFromDb(GalleryDatabase.DataSource, GalleryDatabase.InitialCatalog);
            List<PackageRef> packages;
            using (var connection = await OpenSqlConnectionAsync<GalleryDbConfiguration>())
            {
                packages = (await connection.QueryAsync<PackageRef>(@"
			    SELECT pr.Id, p.NormalizedVersion AS Version, p.Hash, p.LastEdited, p.Published
			    FROM Packages p
			    INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
			    WHERE Published > @cursorDateTime OR LastEdited > @cursorDateTime", new { cursorDateTime = cursorDateTime }))
                    .ToList();
            }
            JobEventSourceLog.GatheredPackagesToArchiveFromDb(packages.Count, GalleryDatabase.DataSource, GalleryDatabase.InitialCatalog);

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

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }
    }
}
