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
using NuGet.Services.Metadata.Catalog.Helpers;
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
                loadData: reader =>
                {
                    var downloadData = new DownloadData();
                    DownloadsV1Reader.Load(reader, downloadData.SetDownloadCount);
                    return downloadData;
                });
        }

        public async Task<HashSet<string>> LoadExcludedPackagesAsync()
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageExcludedPackagesPath,
                reader => JsonStringArrayFileParser.Load(reader, _logger));
        }

        public async Task<IReadOnlyDictionary<string, long>> LoadDownloadOverridesAsync()
        {
            return await LoadAuxiliaryFileAsync(
                _options.Value.AuxiliaryDataStorageDownloadOverridesPath,
                reader => DownloadOverrides.Load(reader, _logger));
        }

        private async Task<T> LoadAuxiliaryFileAsync<T>(
            string blobName,
            Func<JsonReader, T> loadData) where T : class
        {
            // Retry on HTTP 412 because it's possible CloudBlockBlob.OpenReadyAsync throws a 412 while reading the
            // resulting stream. This is because the WindowsAzure.Storage SDK implements the streaming read with a
            // series of range requests with If-Match, presumably to keep memory consumption at a minimum. During these
            // reads, the file can be modified which will cause an HTTP 412 Precondition Failed.
            var data = default(T);
            await Retry.IncrementalAsync(
                async () =>
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
                        data = loadData(jsonReader);
                        stopwatch.Stop();

                        _telemetryService.TrackAuxiliaryFileDownloaded(blobName, stopwatch.Elapsed);
                        _logger.LogInformation(
                            "Loaded blob {BlobName}. Took {Duration}.",
                            blobName,
                            stopwatch.Elapsed);
                    };
                },
                ex => ex is StorageException se && se.IsPreconditionFailedException(),
                maxRetries: 5,
                initialWaitInterval: TimeSpan.Zero,
                waitIncrement: TimeSpan.FromSeconds(10));
            return data;
        }
    }
}
