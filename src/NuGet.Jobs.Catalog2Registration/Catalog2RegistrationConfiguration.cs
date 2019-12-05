// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.V3;

namespace NuGet.Jobs.Catalog2Registration
{
    public class Catalog2RegistrationConfiguration : ICommitCollectorConfiguration
    {
        private static readonly int DefaultMaxConcurrentHivesPerId = Enum.GetValues(typeof(HiveType)).Length;

        /// <summary>
        /// The connection string used to connect to an Azure Blob Storage account. The connection string specifies
        /// the account name, the endpoint suffix (e.g. Azure vs. Azure China), and authentication credential (e.g. storage
        /// key).
        /// </summary>
        public string StorageConnectionString { get; set; }

        /// <summary>
        /// The blob storage container for the legacy hive (not gzipped, no SemVer 2.0.0 packages). This container is in
        /// the Azure Blob Storage account specified in <see cref="StorageConnectionString"/>.
        /// </summary>
        public string LegacyStorageContainer { get; set; }

        /// <summary>
        /// The user-facing base URL for the legacy registration hive.
        /// </summary>
        public string LegacyBaseUrl { get; set; }

        /// <summary>
        /// The blob storage container for the gzipped hive (no SemVer 2.0.0 packages). This container is in the Azure
        /// Blob Storage account specified in <see cref="StorageConnectionString"/>.
        /// </summary>
        public string GzippedStorageContainer { get; set; }

        /// <summary>
        /// The user-facing base URL for the gzipped registration hive.
        /// </summary>
        public string GzippedBaseUrl { get; set; }

        /// <summary>
        /// The blob storage container for the SemVer 2.0.0 hive (gzipped and SemVer 2.0.0 packages). This container is
        /// in the Azure Blob Storage account specified in <see cref="StorageConnectionString"/>.
        /// </summary>
        public string SemVer2StorageContainer { get; set; }

        /// <summary>
        /// The user-facing base URL for the SemVer 2.0.0 registration hive.
        /// </summary>
        public string SemVer2BaseUrl { get; set; }

        /// <summary>
        /// Zero or more URL to the dependency cursor URLs. The registration collector will go no further in the
        /// catalog than these cursors.
        /// </summary>
        public List<string> DependencyCursorUrls { get; set; }

        /// <summary>
        /// The catalog index URL to poll for package details and package deletes.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The flat container base URL (i.e. the package base address) as it appears in the service index. This will
        /// be used to create user-facing URLs so should match the service index. This will be used for generating
        /// .nupkg and package icon URLs.
        /// </summary>
        public string FlatContainerBaseUrl { get; set; }

        /// <summary>
        /// The gallery base URL. This will be used for generating package license URLs.
        /// </summary>
        public string GalleryBaseUrl { get; set; }

        /// <summary>
        /// The maximum number of catalog leafs to download in parallel. When a batch of new catalog leafs is found
        /// in the catalog, the package details leaves for all package IDs are downloaded in parallel. Package delete
        /// leaves are not downloaded and therefore are not relevant to this setting.
        /// </summary>
        public int MaxConcurrentCatalogLeafDownloads { get; set; } = 64;

        /// <summary>
        /// The timeout used for the collector <see cref="System.Net.Http.HttpClient"/>.
        /// </summary>
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Whether or not the registration containers should be created at runtime. In general it is best to allow
        /// ops tools or manual process to create containers so that the public access level can be properly set.
        /// </summary>
        public bool CreateContainers { get; set; }

        /// <summary>
        /// The maximum number of package IDs to process in parallel. This parallelism
        /// can be constrained by <see cref="MaxConcurrentStorageOperations"/> as well.
        /// </summary>
        public int MaxConcurrentIds { get; set; } = 64;

        /// <summary>
        /// The maximum number of hives to process in parallel for a single ID. The other parallelism controls are
        /// probably more interesting so this setting should probably be left at the default (full parallelism) unless
        /// you are trying to force sequential processing in which case it would be set to 1.
        /// </summary>
        public int MaxConcurrentHivesPerId { get; set; } = DefaultMaxConcurrentHivesPerId;

        /// <summary>
        /// The maximum number of asynchronous operations to perform while processing a single hive. This parallelism
        /// can be constrained by <see cref="MaxConcurrentStorageOperations"/> as well.
        /// </summary>
        public int MaxConcurrentOperationsPerHive { get; set; } = 64;

        /// <summary>
        /// The maximum number of blob storage operations (read, write, delete) that can be performed in parallel.
        /// </summary>
        public int MaxConcurrentStorageOperations { get; set; } = 64;

        /// <summary>
        /// The maximum number of registration leaf items to allow in a registration page. If the number of items
        /// exceeds this number, a new page item will be created.
        /// </summary>
        public int MaxLeavesPerPage { get; set; } = 64;

        /// <summary>
        /// The maximum number of leaf items to allow before pages stop being inlined. In other words, if there are
        /// less than or equal to this number of package versions for single ID, pages will be inlined in the
        /// registration index. If there are more than this number of package versions for a single ID, pages will be
        /// written to their own blobs (i.e. they will no longer be inlined in the index).
        /// </summary>
        public int MaxInlinedLeafItems { get; set; } = 127;

        /// <summary>
        /// Whenever a blob storage write is performed, the code will also ensure that there is at least a single
        /// snapshot. The snapshot's content is not important but is present to mitigate accidental deletion via
        /// tooling such as Azure Storage Explorer.
        /// </summary>
        public bool EnsureSingleSnapshot { get; set; } = true;
    }
}
