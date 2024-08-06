// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageService : IFeatureFlagStorageService
    {
        protected static readonly JsonSerializer Serializer;

        protected readonly ICoreFileStorageService _storage;

        static FeatureFlagFileStorageService()
        {
            Serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Error,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter()
                }
            });
        }

        public FeatureFlagFileStorageService(
            ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<FeatureFlags> GetAsync()
        {
            using (var stream = await _storage.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
            {
                return ReadFeatureFlagsFromStream(stream);
            }
        }

        protected FeatureFlags ReadFeatureFlagsFromStream(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return Serializer.Deserialize<FeatureFlags>(reader);
            }
        }
    }
}
