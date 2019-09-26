// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Contains context for <see cref="IValidator"/> when a test is run.
    /// </summary>
    public class ValidationContext
    {
        private readonly IPackageRegistrationMetadataResource _databasePackageRegistrationMetadataResource;
        private readonly IPackageRegistrationMetadataResource _v3PackageRegistrationMetadataResource;
        private readonly HttpSourceResource _httpSourceResource;
        private readonly Lazy<Task<PackageRegistrationIndexMetadata>> _databaseIndex;
        private readonly Lazy<Task<PackageRegistrationIndexMetadata>> _v3Index;
        private readonly Lazy<Task<PackageRegistrationLeafMetadata>> _databaseLeaf;
        private readonly Lazy<Task<PackageRegistrationLeafMetadata>> _v3Leaf;
        private readonly IPackageTimestampMetadataResource _databasetimestampMetadataResource;
        private readonly Lazy<Task<PackageTimestampMetadata>> _timestampMetadataDatabase;
        private readonly ILogger<ValidationContext> _logger;
        private readonly Common.ILogger _commonLogger;

        private readonly ConcurrentDictionary<Uri, Lazy<Task<IEnumerable<IPackageSearchMetadata>>>> _searchPageForIdCache
            = new ConcurrentDictionary<Uri, Lazy<Task<IEnumerable<IPackageSearchMetadata>>>>();

        /// <summary>
        /// The <see cref="PackageIdentity"/> to run the test on.
        /// </summary>
        public PackageIdentity Package { get; }

        /// <summary>
        /// The <see cref="CatalogIndexEntry"/>s for the package that were collected.
        /// </summary>
        /// <remarks>
        /// This can be null.
        /// Most validations are queued with the catalog entries of a package, 
        /// but sometimes we do not know the catalog entries associated with a package but still want to run validations against the current state of V3.
        /// This could happen if a change was never ingested by V3 and there is no catalog entry associated the package.
        /// It could also happen if we lose the catalog entries of a package due to a message processing failure (<see cref="PackageMonitoringStatus.ValidationException"/>).
        /// </remarks>
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
            if (deletionAuditEntries == null)
            {
                throw new ArgumentNullException(nameof(deletionAuditEntries));
            }

            if (sourceRepositories == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositories));
            }

            Package = package ?? throw new ArgumentNullException(nameof(package));
            Entries = entries?.ToList();
            DeletionAuditEntries = deletionAuditEntries.ToList();
            Client = client ?? throw new ArgumentNullException(nameof(client));
            CancellationToken = token;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commonLogger = logger.AsCommon();

            _databasetimestampMetadataResource = sourceRepositories.V2.GetResource<IPackageTimestampMetadataResource>();
            _databasePackageRegistrationMetadataResource = sourceRepositories.V2.GetResource<IPackageRegistrationMetadataResource>();
            _v3PackageRegistrationMetadataResource = sourceRepositories.V3.GetResource<IPackageRegistrationMetadataResource>();
            _httpSourceResource = sourceRepositories.V3.GetResource<HttpSourceResource>();

            _databaseIndex = new Lazy<Task<PackageRegistrationIndexMetadata>>(
                () => _databasePackageRegistrationMetadataResource.GetIndexAsync(Package, _commonLogger, CancellationToken));
            _v3Index = new Lazy<Task<PackageRegistrationIndexMetadata>>(
                () => _v3PackageRegistrationMetadataResource.GetIndexAsync(Package, _commonLogger, CancellationToken));

            _databaseLeaf = new Lazy<Task<PackageRegistrationLeafMetadata>>(
                () => _databasePackageRegistrationMetadataResource.GetLeafAsync(Package, _commonLogger, CancellationToken));
            _v3Leaf = new Lazy<Task<PackageRegistrationLeafMetadata>>(
                () => _v3PackageRegistrationMetadataResource.GetLeafAsync(Package, _commonLogger, CancellationToken));

            _timestampMetadataDatabase = new Lazy<Task<PackageTimestampMetadata>>(
                () => _databasetimestampMetadataResource.GetAsync(this));
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> GetSearchPageForIdAsync(Uri searchBaseUri)
        {
            var lazyTask = _searchPageForIdCache.GetOrAdd(
                searchBaseUri,
                _ => new Lazy<Task<IEnumerable<IPackageSearchMetadata>>>(() =>
                {
                    var queryUri = new Uri(searchBaseUri, "/query");
                    _logger.LogInformation(
                        "Searching for package {Id} on {Uri}.",
                        Package.Id,
                        queryUri.AbsoluteUri);

                    var rawSearchResource = new RawSearchResourceV3(_httpSourceResource.HttpSource, new[] { queryUri });
                    var packageSearchResource = new PackageSearchResourceV3(rawSearchResource);
                    return packageSearchResource.SearchAsync(
                        searchTerm: $"packageid:{Package.Id}",
                        filter: new SearchFilter(includePrerelease: true),
                        skip: 0,
                        take: 1,
                        log: _commonLogger,
                        cancellationToken: CancellationToken);
                }));

            return await lazyTask.Value;
        }

        public Task<PackageRegistrationIndexMetadata> GetIndexDatabaseAsync() => _databaseIndex.Value;
        public Task<PackageRegistrationIndexMetadata> GetIndexV3Async() => _v3Index.Value;
        public Task<PackageRegistrationLeafMetadata> GetLeafDatabaseAsync() => _databaseLeaf.Value;
        public Task<PackageRegistrationLeafMetadata> GetLeafV3Async() => _v3Leaf.Value;
        public Task<PackageTimestampMetadata> GetTimestampMetadataDatabaseAsync() => _timestampMetadataDatabase.Value;
    }
}