// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.FeatureFlags;

namespace NuGet.Services.AzureSearch
{
    public class FeatureFlagService : IFeatureFlagService
    {
        private const string SearchPrefix = "Search.";

        private readonly IFeatureFlagClient _features;

        public FeatureFlagService(IFeatureFlagClient features)
        {
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        public bool IsPopularityTransferEnabled()
        {
            return _features.IsEnabled(SearchPrefix + "TransferPopularity", defaultValue: true);
        }

        public bool IsDeepPagingDisabled()
        {
            return _features.IsEnabled(SearchPrefix + "DisableDeepPaging", defaultValue: false);
        }
    }
}
