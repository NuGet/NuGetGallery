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
    public class OwnerDataClient : IOwnerDataClient
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<OwnerDataClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public OwnerDataClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchJobConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<OwnerDataClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>>> ReadLatestIndexedAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var blobName = GetLatestIndexedBlobName();
            var blobReference = Container.GetBlobReference(blobName);

            _logger.LogInformation("Reading the latest indexed owners from {BlobName}.", blobName);

            var builder = new PackageIdToOwnersBuilder(_logger);
            IAccessCondition accessCondition;
            try
            {
                using (var stream = await blobReference.OpenReadAsync(AccessCondition.GenerateEmptyCondition()))
                {
                    accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(blobReference.ETag);
                    ReadStream(stream, builder.Add);
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                accessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();
                _logger.LogInformation("The blob {BlobName} does not exist.", blobName);
            }

            var output = new ResultAndAccessCondition<SortedDictionary<string, SortedSet<string>>>(
                builder.GetResult(),
                accessCondition);

            stopwatch.Stop();
            _telemetryService.TrackReadLatestIndexedOwners(output.Result.Count, stopwatch.Elapsed);

            return output;
        }

        public async Task UploadChangeHistoryAsync(IReadOnlyList<string> packageIds)
        {
            if (packageIds.Count == 0)
            {
                throw new ArgumentException("The list of package IDs must have at least one element.", nameof(packageIds));
            }

            using (_telemetryService.TrackUploadOwnerChangeHistory(packageIds.Count))
            {
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss-FFFFFFF");
                var blobName = $"{_options.Value.NormalizeStoragePath()}owners/changes/{timestamp}.json";
                _logger.LogInformation("Uploading owner changes to {BlobName}.", blobName);

                var blobReference = Container.GetBlobReference(blobName);

                using (var stream = await blobReference.OpenWriteAsync(AccessCondition.GenerateIfNotExistsCondition()))
                using (var streamWriter = new StreamWriter(stream))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    blobReference.Properties.ContentType = "application/json";
                    Serializer.Serialize(jsonTextWriter, packageIds);
                }
            }
        }

        public async Task ReplaceLatestIndexedAsync(
            SortedDictionary<string, SortedSet<string>> newData,
            IAccessCondition accessCondition)
        {
            using (_telemetryService.TrackReplaceLatestIndexedOwners(newData.Count))
            {
                var blobName = GetLatestIndexedBlobName();
                _logger.LogInformation("Replacing the latest indexed owners from {BlobName}.", blobName);

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

        private static void ReadStream(Stream stream, Action<string, IReadOnlyList<string>> add)
        {
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                Guard.Assert(jsonReader.Read(), "The blob should be readable.");
                Guard.Assert(jsonReader.TokenType == JsonToken.StartObject, "The first token should be the start of an object.");
                Guard.Assert(jsonReader.Read(), "There should be a second token.");
                while (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    var id = (string)jsonReader.Value;

                    Guard.Assert(jsonReader.Read(), "There should be a token after the property name.");
                    Guard.Assert(jsonReader.TokenType == JsonToken.StartArray, "The token after the property name should be the start of an object.");

                    var owners = Serializer.Deserialize<List<string>>(jsonReader);
                    add(id, owners);

                    Guard.Assert(jsonReader.TokenType == JsonToken.EndArray, "The token after reading the array should be the end of an array.");
                    Guard.Assert(jsonReader.Read(), "There should be a token after the end of the array.");
                }

                Guard.Assert(jsonReader.TokenType == JsonToken.EndObject, "The last token should be the end of an object.");
                Guard.Assert(!jsonReader.Read(), "There should be no token after the end of the object.");
            }
        }

        private string GetLatestIndexedBlobName()
        {
            return $"{_options.Value.NormalizeStoragePath()}owners/owners.v2.json";
        }
    }
}

