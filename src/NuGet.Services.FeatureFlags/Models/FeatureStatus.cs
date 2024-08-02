// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.FeatureFlags
{
    /// <summary>
    /// The state of a specific feature. A feature is either completely enabled or disabled.
    /// For example, the "package upload" feature can be disabled if package ingestion is degraded.
    /// </summary>
    public enum FeatureStatus
    {
        /// <summary>
        /// The feature is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// The feature is enabled.
        /// </summary>
        Enabled = 1,
    }
}
