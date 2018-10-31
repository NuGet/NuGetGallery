// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validators
{
    public class ValidationContextTests
    {
        private const string _key = "a";

        private static readonly PackageIdentity _packageIdentity = new PackageIdentity("A", new NuGetVersion(1, 0, 0));

        [Fact]
        public void Constructor_WhenPackageIdentityIsNull_Throws()
        {
            PackageIdentity packageIdentity = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    new CollectorHttpClient(),
                    CancellationToken.None));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenEntriesIsNull_Throws()
        {
            IEnumerable<CatalogIndexEntry> entries = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    entries,
                    Enumerable.Empty<DeletionAuditEntry>(),
                    new CollectorHttpClient(),
                    CancellationToken.None));

            Assert.Equal("entries", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenDeletionAuditEntriesIsNull_Throws()
        {
            IEnumerable<DeletionAuditEntry> deletionAuditEntries = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    deletionAuditEntries,
                    new CollectorHttpClient(),
                    CancellationToken.None));

            Assert.Equal("deletionAuditEntries", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenClientIsNull_Throws()
        {
            CollectorHttpClient client = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidationContext(
                    _packageIdentity,
                    Enumerable.Empty<CatalogIndexEntry>(),
                    Enumerable.Empty<DeletionAuditEntry>(),
                    client,
                    CancellationToken.None));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_InitializesInstance()
        {
            var catalogIndexEntries = Enumerable.Empty<CatalogIndexEntry>();
            var deletionAuditEntries = Enumerable.Empty<DeletionAuditEntry>();
            var client = new CollectorHttpClient();
            var cancellationToken = new CancellationToken(canceled: true);

            var context = new ValidationContext(
                _packageIdentity,
                catalogIndexEntries,
                deletionAuditEntries,
                client,
                cancellationToken);

            Assert.Same(_packageIdentity, context.Package);
            Assert.Equal(catalogIndexEntries.Count(), context.Entries.Count);
            Assert.Equal(deletionAuditEntries.Count(), context.DeletionAuditEntries.Count);
            Assert.Same(client, context.Client);
            Assert.Equal(cancellationToken, context.CancellationToken);
        }

        [Fact]
        public void GetCachedResultAsync_ReturnsMemoizedBoolTask()
        {
            var context = CreateContext();

            var task1 = context.GetCachedResultAsync(_key, new Lazy<Task<bool>>(() => Task.FromResult(true)));
            var task2 = context.GetCachedResultAsync(_key, new Lazy<Task<bool>>(() => Task.FromResult(true)));

            Assert.Same(task1, task2);
        }

        [Fact]
        public void GetCachedResultAsync_ReturnsMemoizedIndexTask()
        {
            var context = CreateContext();

            var task1 = context.GetCachedResultAsync(
                _key,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(
                    () => Task.FromResult<PackageRegistrationIndexMetadata>(null)));
            var task2 = context.GetCachedResultAsync(
                _key,
                new Lazy<Task<PackageRegistrationIndexMetadata>>(
                    () => Task.FromResult<PackageRegistrationIndexMetadata>(null)));

            Assert.Same(task1, task2);
        }

        [Fact]
        public void GetCachedResultAsync_ReturnsMemoizedLeafTask()
        {
            var context = CreateContext();

            var task1 = context.GetCachedResultAsync(
                _key,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(
                    () => Task.FromResult<PackageRegistrationLeafMetadata>(null)));
            var task2 = context.GetCachedResultAsync(
                _key,
                new Lazy<Task<PackageRegistrationLeafMetadata>>(
                    () => Task.FromResult<PackageRegistrationLeafMetadata>(null)));

            Assert.Same(task1, task2);
        }

        private static ValidationContext CreateContext()
        {
            return new ValidationContext(
                _packageIdentity,
                Enumerable.Empty<CatalogIndexEntry>(),
                Enumerable.Empty<DeletionAuditEntry>(),
                new CollectorHttpClient(),
                CancellationToken.None);
        }
    }
}