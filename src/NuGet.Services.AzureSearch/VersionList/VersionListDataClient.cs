// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public class VersionListDataClient : IVersionListDataClient
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore, // Prefer more terse serialization.
            Formatting = Formatting.Indented, // Negligable performance impact but much more readable.
        });

        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;
        private readonly ILogger<VersionListDataClient> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;

        public VersionListDataClient(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            ILogger<VersionListDataClient> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _lazyContainer = new Lazy<ICloudBlobContainer>(
                () => _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer));
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task<ResultAndAccessCondition<VersionListData>> ReadAsync(string id)
        {
            var blobReference = Container.GetBlobReference(GetFileName(id));

            _logger.LogInformation("Reading the version list for package ID {PackageId}.", id);

            VersionListData data;
            IAccessCondition accessCondition;
            try
            {
                using (var stream = await blobReference.OpenReadAsync(AccessCondition.GenerateEmptyCondition()))
                using (var streamReader = new StreamReader(stream))
                using (var jsonTextReader = new JsonTextReader(streamReader))
                {
                    data = Serializer.Deserialize<VersionListData>(jsonTextReader);
                }

                accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(blobReference.ETag);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                data = new VersionListData(new Dictionary<string, VersionPropertiesData>());
                accessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();
            }

            return new ResultAndAccessCondition<VersionListData>(data, accessCondition);
        }

        public async Task ReplaceAsync(string id, VersionListData data, IAccessCondition accessCondition)
        {
            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(
                    stream,
                    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    bufferSize: 1024,
                    leaveOpen: true))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    Serializer.Serialize(jsonTextWriter, data);
                }

                stream.Position = 0;

                _logger.LogInformation("Replacing the version list for package ID {PackageId}.", id);

                var mappedAccessCondition = new AccessCondition
                {
                    IfNoneMatchETag = accessCondition.IfNoneMatchETag,
                    IfMatchETag = accessCondition.IfMatchETag,
                };

                var blobReference = Container.GetBlobReference(GetFileName(id));
                blobReference.Properties.ContentType = "application/json";

                await blobReference.UploadFromStreamAsync(
                    stream,
                    mappedAccessCondition);
            }
        }

        private string GetFileName(string id)
        {
            return $"{_options.Value.NormalizeStoragePath()}version-lists/{id.ToLowerInvariant()}.json";
        }
    }
}
