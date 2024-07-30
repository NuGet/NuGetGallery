// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.FeatureFlags;

namespace NuGet.Jobs.Validation
{
    public class FeatureFlagService : IFeatureFlagService
    {
        private const string ValidationPrefix = "Validation.";

        private readonly IFeatureFlagClient _featureFlagClient;

        public FeatureFlagService(IFeatureFlagClient featureFlagClient)
        {
            _featureFlagClient = featureFlagClient ?? throw new ArgumentNullException(nameof(featureFlagClient));
        }

        public bool IsQueueBackEnabled()
        {
            return _featureFlagClient.IsEnabled(ValidationPrefix + "QueueBack", defaultValue: false);
        }

        public bool IsOrchestratorLeaseEnabled()
        {
            return _featureFlagClient.IsEnabled(ValidationPrefix + "OrchestratorLease", defaultValue: false);
        }

        public bool IsExtraValidationLoggingEnabled()
        {
            return _featureFlagClient.IsEnabled(ValidationPrefix + "ExtraValidation", defaultValue: false);
        }
    }
}
