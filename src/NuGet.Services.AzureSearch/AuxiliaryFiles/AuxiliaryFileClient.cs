// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Indexing;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileClient : IAuxiliaryFileClient
    {
        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<IAuxiliaryDataStorageConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<AuxiliaryFileClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public AuxiliaryFileClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<IAuxiliaryDataStorageConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<AuxiliaryFileClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.AuxiliaryDataStorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<DownloadData> LoadDownloadDataAsync()
        {
            var result = await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageDownloadsPath,
                etag: null,
                loadData: loader =>
                {
                    var downloadData = new DownloadData();

                    Downloads.Load(
                        name: null,
                        loader: loader,
                        addCount: downloadData.SetDownloadCount);

                    return downloadData;
                });

            // Discard the etag and other metadata since this API is only ever used to read the latest data.
            return result.Data;
        }

        public async Task<AuxiliaryFileResult<Downloads>> LoadDownloadsAsync(string etag)
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageDownloadsPath,
                etag,
                loader =>
                {
                    var downloads = new Downloads();
                    downloads.Load(
                        name: null,
                        loader: loader,
                        logger: _logger);
                    return downloads;
                });
        }

        public async Task<AuxiliaryFileResult<HashSet<string>>> LoadVerifiedPackagesAsync(string etag)
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageVerifiedPackagesPath,
                etag,
                loader => JsonStringArrayFileParser.Load(
                    fileName: null,
                    loader: loader,
                    logger: _logger));
        }

        public async Task<AuxiliaryFileResult<HashSet<string>>> LoadExcludedPackagesAsync(string etag)
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageExcludedPackagesPath,
                etag,
                loader => JsonStringArrayFileParser.Load(
                    fileName: null,
                    loader: loader,
                    logger: _logger));
        }

        private async Task<AuxiliaryFileResult<T>> LoadAuxiliaryFileAsync<T>(
            string blobName,
            string etag,
            Func<ILoader, T> loadData) where T : class
        {
            _logger.LogInformation(
                "Attempted to load blob {BlobName} as {TypeName} with etag {ETag}.",
                blobName,
                typeof(T).FullName,
                etag);

            var stopwatch = Stopwatch.StartNew();
            var blob = Container.GetBlobReference(blobName);
            var condition = etag != null ? AccessCondition.GenerateIfNoneMatchCondition(etag) : null;
            try
            {
                using (var stream = await blob.OpenReadAsync(condition))
                using (var textReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    var loader = new LoaderAdapter(jsonReader);
                    var data = loadData(loader);
                    stopwatch.Stop();

                    _telemetryService.TrackAuxiliaryFileDownloaded(blobName, stopwatch.Elapsed);
                    _logger.LogInformation(
                        "Loaded blob {BlobName} with etag {OldETag}. New etag is {NewETag}. Took {Duration}.",
                        blobName,
                        etag,
                        blob.ETag,
                        stopwatch.Elapsed);

                    return new AuxiliaryFileResult<T>(
                        notModified: false,
                        data: data,
                        metadata: new AuxiliaryFileMetadata(
                            lastModified: new DateTimeOffset(blob.LastModifiedUtc, TimeSpan.Zero),
                            loaded: DateTimeOffset.UtcNow,
                            loadDuration: stopwatch.Elapsed,
                            fileSize: blob.Properties.Length,
                            etag: blob.ETag));
                };
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotModified)
            {
                stopwatch.Stop();

                _telemetryService.TrackAuxiliaryFileNotModified(blobName, stopwatch.Elapsed);
                _logger.LogInformation(
                    "Blob {BlobName} has not changed from the previous etag {ETag}. Took {Duration}.",
                    blobName,
                    etag,
                    stopwatch.Elapsed);

                return new AuxiliaryFileResult<T>(
                    notModified: true,
                    data: null,
                    metadata: null);
            }
        }

        /// <summary>
        /// This is an adapter implementation so that we can use the pre-existing auxiliary file reading code. It simply
        /// returns a <see cref="JsonReader"/> provided to the constructor and performs no additional network requests.
        /// </summary>
        private class LoaderAdapter : ILoader
        {
            private readonly JsonReader _jsonReader;

            public LoaderAdapter(JsonReader jsonReader)
            {
                _jsonReader = jsonReader ?? throw new ArgumentNullException(nameof(jsonReader));
            }

            public DateTime? GetLastUpdateTime(string name) => throw new NotImplementedException();
            public bool Reload(IndexingConfiguration config) => throw new NotImplementedException();

            public JsonReader GetReader(string name)
            {
                if (name != null)
                {
                    throw new ArgumentException("The provided blob name should be null.", nameof(name));
                }

                return _jsonReader;
            }
        }
    }
}
