// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.FeatureFlags
{
    public class FeatureFlagOptions
    {
        /// <summary>
        /// How frequently the feature flags should be refreshed.
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}
