// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Contains context for <see cref="IValidator"/> when a test is run.
    /// </summary>
    public class ValidationContext
    {
        private readonly IPackageRegistrationMetadataResource _v2PackageRegistrationMetadataResource;
        private readonly IPackageRegistrationMetadataResource _v3PackageRegistrationMetadataResource;
        private readonly Lazy<Task<PackageRegistrationIndexMetadata>> _v2Index;
        private readonly Lazy<Task<PackageRegistrationIndexMetadata>> _v3Index;
        private readonly Lazy<Task<PackageRegistrationLeafMetadata>> _v2Leaf;
        private readonly Lazy<Task<PackageRegistrationLeafMetadata>> _v3Leaf;
        private readonly IPackageTimestampMetadataResource _v2timestampMetadataResource;
        private readonly Lazy<Task<PackageTimestampMetadata>> _timestampMetadataV2;

        /// <summary>
        /// The <see cref="PackageIdentity"/> to run the test on.
        /// </summary>
        public PackageIdentity Package { get; }

        /// <summary>
        /// The <see cref="CatalogIndexEntry"/>s for the package that were collected.
        /// </summary>
        public IReadOnlyList<CatalogIndexEntry> Entries { get; }

        /// <summary>
        /// The <see cref="AuditRecordHelpers.DeletionAuditEntry"/>s, if any are associated with the <see cref="PackageIdentity"/>.
        /// </summary>
        public IReadOnlyList<DeletionAuditEntry> DeletionAuditEntries { get; }

        /// <summary>
        /// The <see cref="CollectorHttpClient"/> to use when needed.
        /// </summary>
        public CollectorHttpClient Client { get; }

        /// <summary>
        /// A <see cref="CancellationToken"/> associated with this run of the test.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        public ValidationContext(
            PackageIdentity package,
            IEnumerable<CatalogIndexEntry> entries,
            IEnumerable<DeletionAuditEntry> deletionAuditEntries,
            ValidationSourceRepositories sourceRepositories,
            CollectorHttpClient client,
            CancellationToken token,
            ILogger<ValidationContext> logger)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (deletionAuditEntries == null)
            {
                throw new ArgumentNullException(nameof(deletionAuditEntries));
            }

            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Package = package ?? throw new ArgumentNullException(nameof(package));
            Entries = entries.ToList();
            DeletionAuditEntries = deletionAuditEntries.ToList();
            Client = client ?? throw new ArgumentNullException(nameof(client));
            CancellationToken = token;

            _v2timestampMetadataResource = sourceRepositories.V2.GetResource<IPackageTimestampMetadataResource>();
            _v2PackageRegistrationMetadataResource = sourceRepositories.V2.GetResource<IPackageRegistrationMetadataResource>();
            _v3PackageRegistrationMetadataResource = sourceRepositories.V3.GetResource<IPackageRegistrationMetadataResource>();

            var commonLogger = logger.AsCommon();

            _v2Index = new Lazy<Task<PackageRegistrationIndexMetadata>>(
                () => _v2PackageRegistrationMetadataResource.GetIndexAsync(Package, commonLogger, CancellationToken));
            _v3Index = new Lazy<Task<PackageRegistrationIndexMetadata>>(
                () => _v3PackageRegistrationMetadataResource.GetIndexAsync(Package, commonLogger, CancellationToken));

            _v2Leaf = new Lazy<Task<PackageRegistrationLeafMetadata>>(
                () => _v2PackageRegistrationMetadataResource.GetLeafAsync(Package, commonLogger, CancellationToken));
            _v3Leaf = new Lazy<Task<PackageRegistrationLeafMetadata>>(
                () => _v3PackageRegistrationMetadataResource.GetLeafAsync(Package, commonLogger, CancellationToken));

            _timestampMetadataV2 = new Lazy<Task<PackageTimestampMetadata>>(
                () => _v2timestampMetadataResource.GetAsync(this));
        }

        public Task<PackageRegistrationIndexMetadata> GetIndexV2Async() => _v2Index.Value;
        public Task<PackageRegistrationIndexMetadata> GetIndexV3Async() => _v3Index.Value;
        public Task<PackageRegistrationLeafMetadata> GetLeafV2Async() => _v2Leaf.Value;
        public Task<PackageRegistrationLeafMetadata> GetLeafV3Async() => _v3Leaf.Value;
        public Task<PackageTimestampMetadata> GetTimestampMetadataV2Async() => _timestampMetadataV2.Value;
    }
}