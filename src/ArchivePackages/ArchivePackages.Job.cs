// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGetGallery;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using Autofac.Core;

namespace ArchivePackages
{
    public class Job : JsonConfigurationJob
    {
        private const string ContentTypeJson = "application/json";
        private const string DateTimeFormatSpecifier = "O";
        private const string CursorDateTimeKey = "cursorDateTime";
        private const string DefaultPackagesContainerName = "packages";
        private const string DefaultPackagesArchiveContainerName = "ng-backups";
        private const string DefaultCursorBlobName = "cursor.json";

        private readonly JobEventSource _jobEventSourceLog = JobEventSource.Log;
        private InitializationConfiguration _configuration;

        private string _sourceContainerName;
        private string _destinationContainerName;

        private string _sourceAccount;
        private string _primaryDestinationAccount;
        private string _secondaryDestinationAccount;

        private ICloudBlobContainer _sourceContainer;
        private ICloudBlobContainer _primaryDestinationContainer;
        private ICloudBlobContainer _secondaryDestinationContainer;

        private string _cursorBlobName;

        /// <summary>
        /// Gallery database registration, for diagnostics.
        /// </summary>
        private SqlConnectionStringBuilder _galleryDatabase;


        public Job() : base(JobEventSource.Log) { }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;

            _galleryDatabase = GetDatabaseRegistration<GalleryDbConfiguration>();

            _sourceContainerName = _configuration.SourceContainerName ?? DefaultPackagesContainerName;
            _sourceAccount = _configuration.Source;

            var sourceBlobClient = _serviceProvider.CreateCloudBlobClient(
                $"BlobEndPoint=https://{_sourceAccount}.blob.core.windows.net");
            _sourceContainer = sourceBlobClient.GetContainerReference(_sourceContainerName);

            _destinationContainerName = _configuration.DestinationContainerName ?? DefaultPackagesArchiveContainerName;
            _primaryDestinationAccount = _configuration.PrimaryDestination;
            var primaryDestinationBlobClient = _serviceProvider.CreateCloudBlobClient(
                $"BlobEndPoint=https://{_primaryDestinationAccount}.blob.core.windows.net");
            _primaryDestinationContainer = primaryDestinationBlobClient.GetContainerReference(_destinationContainerName);

            if (!string.IsNullOrEmpty(_configuration.SecondaryDestination))
            {
                _secondaryDestinationAccount = _configuration.SecondaryDestination;
                var secondaryDestinationBlobClient = _serviceProvider.CreateCloudBlobClient(
                    $"BlobEndPoint=https://{_secondaryDestinationAccount}.blob.core.windows.net");
                _secondaryDestinationContainer = secondaryDestinationBlobClient.GetContainerReference(_destinationContainerName);
            }

            _cursorBlobName = _configuration.CursorBlob ?? DefaultCursorBlobName;
        }

        public override async Task Run()
        {
            _jobEventSourceLog.PreparingToArchive(_sourceAccount, _sourceContainerName, _primaryDestinationAccount, _destinationContainerName, _galleryDatabase.DataSource, _galleryDatabase.InitialCatalog);
            await Archive(_primaryDestinationContainer);

            // todo: consider reusing package query for primary and secondary archives
            if (_secondaryDestinationContainer != null)
            {
                _jobEventSourceLog.PreparingToArchive2(_secondaryDestinationAccount, _destinationContainerName);
                await Archive(_secondaryDestinationContainer);
            }
        }

        private static async Task<JObject> GetJObject(ICloudBlobContainer container, string blobName)
        {
            var blob = container.GetBlobReference(blobName);
            var json = await blob.DownloadTextIfExistsAsync();
            return JObject.Parse(json);
        }

        private static async Task SetJObject(ICloudBlobContainer container, string blobName, JObject jObject)
        {
            var blob = container.GetBlobReference(blobName);
            blob.Properties.ContentType = ContentTypeJson;
            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(jObject.ToString())))
            {
                await blob.UploadFromStreamAsync(stream, overwrite: true);
            }
        }

        private async Task Archive(ICloudBlobContainer destinationContainer)
        {
            var cursorJObject = await GetJObject(destinationContainer, _cursorBlobName);
            var cursorDateTime = cursorJObject[CursorDateTimeKey].Value<DateTime>();

            _jobEventSourceLog.CursorData(cursorDateTime.ToString(DateTimeFormatSpecifier));

            _jobEventSourceLog.GatheringPackagesToArchiveFromDb(_galleryDatabase.DataSource, _galleryDatabase.InitialCatalog);
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
            _jobEventSourceLog.GatheredPackagesToArchiveFromDb(packages.Count, _galleryDatabase.DataSource, _galleryDatabase.InitialCatalog);

            var archiveSet = packages
                .AsParallel()
                .Select(r => Tuple.Create(StorageHelpers.GetPackageBlobName(r.Id, r.Version), StorageHelpers.GetPackageBackupBlobName(r.Id, r.Version, r.Hash)))
                .ToList();

            await destinationContainer.CreateIfNotExistAsync(enablePublicAccess: false);

            if (archiveSet.Count > 0)
            {
                _jobEventSourceLog.StartingArchive(archiveSet.Count);
                foreach (var archiveItem in archiveSet)
                {
                    await ArchivePackage(archiveItem.Item1, archiveItem.Item2, _sourceContainer, destinationContainer);
                }

                var maxLastEdited = packages.Max(p => p.LastEdited);
                var maxPublished = packages.Max(p => p.Published);

                // Time is ever increasing after all, simply store the max of published and lastEdited as cursorDateTime
                var newCursorDateTime = maxLastEdited > maxPublished ? new DateTime(maxLastEdited.Value.Ticks, DateTimeKind.Utc) : new DateTime(maxPublished.Value.Ticks, DateTimeKind.Utc);
                var newCursorDateTimeString = newCursorDateTime.ToString(DateTimeFormatSpecifier);

                _jobEventSourceLog.NewCursorData(newCursorDateTimeString);
                cursorJObject[CursorDateTimeKey] = newCursorDateTimeString;
                await SetJObject(destinationContainer, _cursorBlobName, cursorJObject);
            }
        }

        private async Task ArchivePackage(string sourceBlobName, string destinationBlobName, ICloudBlobContainer sourceContainer, ICloudBlobContainer destinationContainer)
        {
            // Identify the source and destination blobs
            var sourceBlob = sourceContainer.GetBlobReference(sourceBlobName);
            var destBlob = destinationContainer.GetBlobReference(destinationBlobName);

            if (await destBlob.ExistsAsync())
            {
                _jobEventSourceLog.ArchiveExists(destBlob.Name);
            }
            else if (!await sourceBlob.ExistsAsync())
            {
                _jobEventSourceLog.SourceBlobMissing(sourceBlob.Name);
            }
            else
            {
                // Start the copy
                _jobEventSourceLog.StartingCopy(sourceBlob.Name, destBlob.Name);
                await destBlob.StartCopyAsync(sourceBlob, sourceAccessCondition: AccessConditionWrapper.GenerateEmptyCondition(), destAccessCondition: AccessConditionWrapper.GenerateEmptyCondition());
                _jobEventSourceLog.StartedCopy(sourceBlob.Name, destBlob.Name);
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
