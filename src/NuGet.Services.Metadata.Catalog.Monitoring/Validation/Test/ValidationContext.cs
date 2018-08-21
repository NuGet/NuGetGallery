// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Contains context for <see cref="IValidator"/> when a test is run.
    /// </summary>
    public class ValidationContext
    {
        private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _boolCache;
        private readonly ConcurrentDictionary<string, Lazy<Task<PackageRegistrationIndexMetadata>>> _indexCache;
        private readonly ConcurrentDictionary<string, Lazy<Task<PackageRegistrationLeafMetadata>>> _leafCache;

        /// <summary>
        /// The <see cref="PackageIdentity"/> to run the test on.
        /// </summary>
        public PackageIdentity Package { get; }

        /// <summary>
        /// The <see cref="CatalogIndexEntry"/>s for the package that were collected.
        /// </summary>
        public IEnumerable<CatalogIndexEntry> Entries { get; }

        /// <summary>
        /// The <see cref="AuditRecordHelpers.DeletionAuditEntry"/>s, if any are associated with the <see cref="PackageIdentity"/>.
        /// </summary>
        public IEnumerable<DeletionAuditEntry> DeletionAuditEntries { get; }

        /// <summary>
        /// The <see cref="CollectorHttpClient"/> to use when needed.
        /// </summary>
        public CollectorHttpClient Client { get; }

        /// <summary>
        /// A <see cref="CancellationToken"/> associated with this run of the test.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Empty constructor for testing.
        /// </summary>
        public ValidationContext()
        {
            _boolCache = new ConcurrentDictionary<string, Lazy<Task<bool>>>();
            _indexCache = new ConcurrentDictionary<string, Lazy<Task<PackageRegistrationIndexMetadata>>>();
            _leafCache = new ConcurrentDictionary<string, Lazy<Task<PackageRegistrationLeafMetadata>>>();
        }

        public ValidationContext(
            PackageIdentity package,
            IEnumerable<CatalogIndexEntry> entries,
            IEnumerable<DeletionAuditEntry> deletionAuditEntries,
            CollectorHttpClient client,
            CancellationToken token)
            : this()
        {
            Package = package;
            Entries = entries;
            DeletionAuditEntries = deletionAuditEntries;
            Client = client;
            CancellationToken = token;
        }

        public Task<bool> GetCachedResultAsync(string key, Lazy<Task<bool>> lazyTask)
        {
            return _boolCache.GetOrAdd(key, lazyTask).Value;
        }

        public Task<PackageRegistrationIndexMetadata> GetCachedResultAsync(
            string key,
            Lazy<Task<PackageRegistrationIndexMetadata>> lazyTask)
        {
            return _indexCache.GetOrAdd(key, lazyTask).Value;
        }

        public Task<PackageRegistrationLeafMetadata> GetCachedResultAsync(
            string key,
            Lazy<Task<PackageRegistrationLeafMetadata>> lazyTask)
        {
            return _leafCache.GetOrAdd(key, lazyTask).Value;
        }
    }
}