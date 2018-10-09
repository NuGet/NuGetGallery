// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public class VersionListDataClient
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore, // Prefer more terse serialization.
            Formatting = Formatting.Indented, // Negligable performance impact but much more readable.
        });
        private readonly ICoreFileStorageService _storageService;

        public VersionListDataClient(ICoreFileStorageService storageService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        public async Task<ResultAndAccessCondition<VersionListData>> ReadAsync(string id)
        {
            var fileReference = await _storageService.GetFileReferenceAsync(
                CoreConstants.ContentFolderName,
                GetFileName(id));

            VersionListData data;
            IAccessCondition accessCondition;
            if (fileReference == null)
            {
                data = new VersionListData(new Dictionary<string, VersionPropertiesData>());
                accessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();
            }
            else
            {
                using (var stream = fileReference.OpenRead())
                using (var streamReader = new StreamReader(stream))
                using (var jsonTextReader = new JsonTextReader(streamReader))
                {
                    data = Serializer.Deserialize<VersionListData>(jsonTextReader);
                }

                accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(fileReference.ContentId);
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

                await _storageService.SaveFileAsync(
                    CoreConstants.ContentFolderName,
                    GetFileName(id),
                    stream,
                    accessCondition);
            }
        }

        private static string GetFileName(string id)
        {
            return $"version-lists/{id.ToLowerInvariant()}.json";
        }
    }
}
