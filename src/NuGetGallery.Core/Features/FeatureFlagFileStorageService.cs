// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageService : IEditableFeatureFlagStorageService
    {
        private readonly ICoreFileStorageService _storage;
        private readonly JsonSerializer _serializer;

        public FeatureFlagFileStorageService(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error
            });
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

            string json;
            using (var stream = reference.OpenRead())
            using (var streamReader = new StreamReader(stream))
            {
                json = await streamReader.ReadToEndAsync();
            }

            return new FeatureFlagReference(
                PrettifyJson(json),
                reference.ContentId);
        }

        public async Task<FeatureFlagSaveResult> TrySaveAsync(string flags, string contentId)
        {
            // Ensure the feature flags are valid before saving them.
            var validationResult = ValidateFlagsOrNull(flags);
            if (validationResult != null)
            {
                return validationResult;
            }

            var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(contentId);

            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(PrettifyJson(flags));
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

        private FeatureFlagSaveResult ValidateFlagsOrNull(string flags)
        {
            try
            {
                using (var reader = new StringReader(flags))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    _serializer.Deserialize<FeatureFlags>(jsonReader);
                    return null;
                }
            }
            catch (JsonException e)
            {
                return FeatureFlagSaveResult.Invalid(e.Message);
            }
        }

        private string PrettifyJson(string json)
        {
            return JToken.Parse(json).ToString(Formatting.Indented);
        }
    }
}
