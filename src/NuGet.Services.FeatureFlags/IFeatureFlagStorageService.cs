// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.FeatureFlags
{
    public interface IFeatureFlagStorageService
    {
        /// <summary>
        /// Download and parse the latest feature flags state from storage.
        /// </summary>
        /// <returns>The latest feature flags.</returns>
        Task<FeatureFlags> GetAsync();
    }
}
