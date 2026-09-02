// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Configures the storage accounts used during staged package promotion.
    /// </summary>
    public class PromotionConfiguration
    {
        /// <summary>
        /// Gets or sets the connection string for public package storage.
        /// </summary>
        public string PackageStorageConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the connection string for private staging storage.
        /// </summary>
        public string StagingStorageConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the connection string for public flat-container content storage.
        /// </summary>
        public string FlatContainerStorageConnectionString { get; set; }
    }
}
