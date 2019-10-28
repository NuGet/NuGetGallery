// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private readonly IOptionsSnapshot<AuxiliaryDataStorageConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<AuxiliaryFileClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public AuxiliaryFileClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AuxiliaryDataStorageConfiguration> options,
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
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageDownloadsPath,
                loadData: loader =>
                {
                    var downloadData = new DownloadData();

                    Downloads.Load(
                        name: null,
                        loader: loader,
                        addCount: downloadData.SetDownloadCount);

                    return downloadData;
                });
        }

        public async Task<HashSet<string>> LoadVerifiedPackagesAsync()
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageVerifiedPackagesPath,
                loader => JsonStringArrayFileParser.Load(
                    fileName: null,
                    loader: loader,
                    logger: _logger));
        }

        public async Task<HashSet<string>> LoadExcludedPackagesAsync()
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageExcludedPackagesPath,
                loader => JsonStringArrayFileParser.Load(
                    fileName: null,
                    loader: loader,
                    logger: _logger));
        }

        public async Task<IReadOnlyDictionary<string, long>> LoadDownloadOverridesAsync()
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageDownloadOverridesPath,
                loader => DownloadOverrides.Load(
                    fileName: null,
                    loader: loader,
                    logger: _logger));
        }

        private async Task<T> LoadAuxiliaryFileAsync<T>(
            string blobName,
            Func<ILoader, T> loadData) where T : class
        {
            _logger.LogInformation(
                "Attempted to load blob {BlobName} as {TypeName}.",
                blobName,
                typeof(T).FullName);

            var stopwatch = Stopwatch.StartNew();
            var blob = Container.GetBlobReference(blobName);
            using (var stream = await blob.OpenReadAsync(AccessCondition.GenerateEmptyCondition()))
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                var loader = new LoaderAdapter(jsonReader);
                var data = loadData(loader);
                stopwatch.Stop();

                _telemetryService.TrackAuxiliaryFileDownloaded(blobName, stopwatch.Elapsed);
                _logger.LogInformation(
                    "Loaded blob {BlobName}. Took {Duration}.",
                    blobName,
                    stopwatch.Elapsed);

                return data;
            };
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
