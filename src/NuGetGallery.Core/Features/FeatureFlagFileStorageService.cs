// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageService : IFeatureFlagStorageService
    {
        private readonly ICoreFileStorageService _storage;
        private readonly FeatureFlagOptions _options;
        private readonly JsonSerializer _serializer;

        public FeatureFlagFileStorageService(ICoreFileStorageService storage, FeatureFlagOptions options)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _options = options ?? throw new ArgumentNullException(nameof(options));
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
    }
}
