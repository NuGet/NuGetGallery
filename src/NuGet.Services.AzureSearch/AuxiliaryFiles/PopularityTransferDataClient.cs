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
    public class PopularityTransferDataClient : IPopularityTransferDataClient
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<PopularityTransferDataClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public PopularityTransferDataClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<PopularityTransferDataClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<AuxiliaryFileResult<PopularityTransferData>> ReadLatestIndexedAsync(
            IAccessCondition accessCondition,
            StringCache stringCache)
        {
            var stopwatch = Stopwatch.StartNew();
            var blobName = GetLatestIndexedBlobName();
            var blobReference = Container.GetBlobReference(blobName);

            _logger.LogInformation("Reading the latest indexed popularity transfers from {BlobName}.", blobName);

            bool modified;
            var data = new PopularityTransferData();
            AuxiliaryFileMetadata metadata;
            try
            {
                using (var stream = await blobReference.OpenReadAsync(accessCondition))
                {
                    ReadStream(stream, (from, to) => data.AddTransfer(stringCache.Dedupe(from), stringCache.Dedupe(to)));
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
            _telemetryService.TrackReadLatestIndexedPopularityTransfers(data?.Count, modified, stopwatch.Elapsed);

            return new AuxiliaryFileResult<PopularityTransferData>(
                modified,
                data,
                metadata);
        }

        public async Task ReplaceLatestIndexedAsync(
            PopularityTransferData newData,
            IAccessCondition accessCondition)
        {
            using (_telemetryService.TrackReplaceLatestIndexedPopularityTransfers(newData.Count))
            {
                var blobName = GetLatestIndexedBlobName();
                _logger.LogInformation("Replacing the latest indexed popularity transfers from {BlobName}.", blobName);

                var mappedAccessCondition = new AccessCondition
                {
                    IfNoneMatchETag = accessCondition.IfNoneMatchETag,
                    IfMatchETag = accessCondition.IfMatchETag,
                };

                var blobReference = Container.GetBlobReference(blobName);

                using (var stream = await blobReference.OpenWriteAsync(mappedAccessCondition))
                using (var streamWriter = new StreamWriter(stream))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    blobReference.Properties.ContentType = "application/json";
                    Serializer.Serialize(jsonTextWriter, newData);
                }
            }
        }

        private static void ReadStream(Stream stream, Action<string, string> add)
        {
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                Guard.Assert(jsonReader.Read(), "The blob should be readable.");
                Guard.Assert(jsonReader.TokenType == JsonToken.StartObject, "The first token should be the start of an object.");
                Guard.Assert(jsonReader.Read(), "There should be a second token.");

                while (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    var fromId = (string)jsonReader.Value;

                    Guard.Assert(jsonReader.Read(), "There should be a token after the property name.");
                    Guard.Assert(jsonReader.TokenType == JsonToken.StartArray, "The token after the property name should be the start of an array.");
                    Guard.Assert(jsonReader.Read(), "There should be a token after the start of the transfer array.");

                    while (jsonReader.TokenType == JsonToken.String)
                    {
                        add(fromId, (string)jsonReader.Value);

                        Guard.Assert(jsonReader.Read(), "There should be a token after the 'to' package ID.");
                    }

                    Guard.Assert(jsonReader.TokenType == JsonToken.EndArray, "The token after reading the array should be the end of an array.");
                    Guard.Assert(jsonReader.Read(), "There should be a token after the end of the array.");
                }

                Guard.Assert(jsonReader.TokenType == JsonToken.EndObject, "The last token should be the end of an object.");
                Guard.Assert(!jsonReader.Read(), "There should be no token after the end of the object.");
            }
        }

        private string GetLatestIndexedBlobName()
        {
            return $"{_options.Value.NormalizeStoragePath()}popularity-transfers/popularity-transfers.v1.json";
        }
    }
}

