// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadDataClient : IDownloadDataClient
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<DownloadDataClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public DownloadDataClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<DownloadDataClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<AuxiliaryFileResult<DownloadData>> ReadLatestIndexedAsync(
            IAccessCondition accessCondition,
            StringCache stringCache)
        {
            var stopwatch = Stopwatch.StartNew();
            var blobName = GetLatestIndexedBlobName();
            var blobReference = Container.GetBlobReference(blobName);

            _logger.LogInformation("Reading the latest indexed downloads from {BlobName}.", blobName);

            bool modified;
            var downloads = new DownloadData();
            AuxiliaryFileMetadata metadata;
            try
            {
                using (var stream = await blobReference.OpenReadAsync(accessCondition))
                {
                    ReadStream(
                        stream,
                        (id, version, downloadCount) =>
                        {
                            id = stringCache.Dedupe(id);
                            version = stringCache.Dedupe(version);
                            downloads.SetDownloadCount(id, version, downloadCount);
                        });
                    modified = true;
                    metadata = new AuxiliaryFileMetadata(
                        lastModified: new DateTimeOffset(blobReference.LastModifiedUtc, TimeSpan.Zero),
                        loadDuration: stopwatch.Elapsed,
                        fileSize: blobReference.Properties.Length,
                        etag: blobReference.ETag);
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotModified)
            {
                _logger.LogInformation("The blob {BlobName} has not changed.", blobName);
                modified = false;
                downloads = null;
                metadata = null;
            }

            stopwatch.Stop();
            _telemetryService.TrackReadLatestIndexedDownloads(downloads?.Count, modified, stopwatch.Elapsed);

            return new AuxiliaryFileResult<DownloadData>(
                modified,
                downloads,
                metadata);
        }

        public async Task ReplaceLatestIndexedAsync(
            DownloadData newData,
            IAccessCondition accessCondition)
        {
            using (_telemetryService.TrackReplaceLatestIndexedDownloads(newData.Count))
            {
                var blobName = GetLatestIndexedBlobName();
                _logger.LogInformation("Replacing the latest indexed downloads from {BlobName}.", blobName);

                var blobReference = Container.GetBlobReference(blobName);

                using (var stream = await blobReference.OpenWriteAsync(accessCondition))
                using (var streamWriter = new StreamWriter(stream))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    blobReference.Properties.ContentType = "application/json";
                    Serializer.Serialize(jsonTextWriter, newData);
                }
            }
        }

        private static void ReadStream(
            Stream stream,
            Action<string, string, long> addVersion)
        {
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                Guard.Assert(jsonReader.Read(), "The blob should be readable.");
                Guard.Assert(jsonReader.TokenType == JsonToken.StartObject, "The first token should be the start of an object.");
                Guard.Assert(jsonReader.Read(), "There should be a second token.");

                while (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    // We assume the package ID has valid characters.
                    var id = (string)jsonReader.Value;

                    Guard.Assert(jsonReader.Read(), "There should be a token after the package ID.");
                    Guard.Assert(jsonReader.TokenType == JsonToken.StartObject, "The token after the package ID should be the start of an object.");
                    Guard.Assert(jsonReader.Read(), "There should be a token after the start of the ID object.");

                    while (jsonReader.TokenType == JsonToken.PropertyName)
                    {
                        // We assume the package version is already normalized.
                        var version = (string)jsonReader.Value;

                        Guard.Assert(jsonReader.Read(), "There should be a token after the package version.");
                        Guard.Assert(jsonReader.TokenType == JsonToken.Integer, "The token after the package version should be an integer.");

                        var downloads = (long)jsonReader.Value;

                        Guard.Assert(jsonReader.Read(), "There should be a token after the download count.");

                        addVersion(id, version, downloads);
                    }

                    Guard.Assert(jsonReader.TokenType == JsonToken.EndObject, "The token after the package versions should be the end of an object.");
                    Guard.Assert(jsonReader.Read(), "There should be a token after the package ID object.");
                }

                Guard.Assert(jsonReader.TokenType == JsonToken.EndObject, "The last token should be the end of an object.");
                Guard.Assert(!jsonReader.Read(), "There should be no token after the end of the object.");
            }
        }

        private string GetLatestIndexedBlobName()
        {
            return $"{_options.Value.NormalizeStoragePath()}downloads/downloads.v2.json";
        }
    }
}

