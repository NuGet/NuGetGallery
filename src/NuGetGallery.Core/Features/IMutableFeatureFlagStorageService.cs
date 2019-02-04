// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Features
{
    public interface IMutableFeatureFlagStorageService : IFeatureFlagStorageService
    {
        Task<FeatureFlagReference> GetReferenceAsync();

        Task<FeatureFlagSaveResult> TrySaveAsync(string flags, string contentId);
    }
}
