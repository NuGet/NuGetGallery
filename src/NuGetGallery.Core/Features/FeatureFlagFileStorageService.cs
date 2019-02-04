// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageService : IMutableFeatureFlagStorageService
    {
        private readonly ICoreFileStorageService _storage;
        private readonly JsonSerializer _serializer;

        public FeatureFlagFileStorageService(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = new JsonSerializer();
        }

        public async Task<FeatureFlags> GetAsync()
        {
            using (var stream = await _storage.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return _serializer.Deserialize<FeatureFlags>(reader);
            }
        }

        public async Task<FeatureFlagReference> GetReferenceAsync()
        {
            var reference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName);

            using (var stream = reference.OpenRead())
            using (var streamReader = new StreamReader(stream))
            {
                return new FeatureFlagReference(streamReader.ReadToEnd(), reference.ContentId);
            }
        }

        public async Task<FeatureFlagSaveResult> TrySaveAsync(string flags, string contentId)
        {
            // Ensure the feature flags are valid before saving them.
            if (!IsValidFlagsJson(flags))
            {
                return FeatureFlagSaveResult.Invalid;
            }

            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(contentId);

            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(flags);
                    writer.Flush();
                    stream.Position = 0;

                    await _storage.SaveFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, stream, accessCondition);

                    return FeatureFlagSaveResult.Ok;
                }
            }
            catch (StorageException e) when (e.IsPreconditionFailedException())
            {
                return FeatureFlagSaveResult.Conflict;
            }
        }

        public static bool IsValidFlagsJson(string flags)
        {
            try
            {
                JsonConvert.DeserializeObject<FeatureFlags>(flags);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
