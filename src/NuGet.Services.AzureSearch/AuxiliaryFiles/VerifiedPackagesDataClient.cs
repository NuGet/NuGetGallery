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
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class VerifiedPackagesDataClient : IVerifiedPackagesDataClient
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<VerifiedPackagesDataClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public VerifiedPackagesDataClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<VerifiedPackagesDataClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<AuxiliaryFileResult<HashSet<string>>> ReadLatestAsync(
            IAccessCondition accessCondition,
            StringCache stringCache)
        {
            var stopwatch = Stopwatch.StartNew();
            var blobName = GetLatestIndexedBlobName();
            var blobReference = Container.GetBlobReference(blobName);

            _logger.LogInformation("Reading the latest verified packages from {BlobName}.", blobName);

            bool modified;
            var data = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AuxiliaryFileMetadata metadata;
            try
            {
                using (var stream = await blobReference.OpenReadAsync(accessCondition))
                {
                    ReadStream(stream, id => data.Add(stringCache.Dedupe(id)));
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
                data = null;
                metadata = null;
            }

            stopwatch.Stop();
            _telemetryService.TrackReadLatestVerifiedPackages(data?.Count, modified, stopwatch.Elapsed);

            return new AuxiliaryFileResult<HashSet<string>>(
                modified,
                data,
                metadata);
        }

        public async Task ReplaceLatestAsync(
            HashSet<string> newData,
            IAccessCondition accessCondition)
        {
            using (_telemetryService.TrackReplaceLatestVerifiedPackages(newData.Count))
            {
                var blobName = GetLatestIndexedBlobName();
                _logger.LogInformation("Replacing the latest verified packages from {BlobName}.", blobName);

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

        private static void ReadStream(Stream stream, Action<string> add)
        {
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                Guard.Assert(jsonReader.Read(), "The blob should be readable.");
                Guard.Assert(jsonReader.TokenType == JsonToken.StartArray, "The first token should be the start of an array.");
                Guard.Assert(jsonReader.Read(), "There should be a second token.");
                while (jsonReader.TokenType == JsonToken.String)
                {
                    var id = (string)jsonReader.Value;
                    add(id);

                    Guard.Assert(jsonReader.Read(), "There should be a token after the string.");
                }

                Guard.Assert(jsonReader.TokenType == JsonToken.EndArray, "The last token should be the end of an array.");
                Guard.Assert(!jsonReader.Read(), "There should be no token after the end of the array.");
            }
        }

        private string GetLatestIndexedBlobName()
        {
            return $"{_options.Value.NormalizeStoragePath()}verified-packages/verified-packages.v1.json";
        }
    }
}
