// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

        [Fact]
        public void DefaultConstructor_InitializesProperties()
        {
            var context = new ValidationContext();

            Assert.Null(context.Package);
            Assert.Null(context.Entries);
            Assert.Null(context.DeletionAuditEntries);
            Assert.Null(context.Client);
            Assert.Equal(CancellationToken.None, context.CancellationToken);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var package = new PackageIdentity("A", new NuGetVersion(1, 0, 0));
            var catalogIndexEntries = Enumerable.Empty<CatalogIndexEntry>();
            var deletionAuditEntries = Enumerable.Empty<DeletionAuditEntry>();
            var client = new CollectorHttpClient();
            var cancellationToken = new CancellationToken(canceled: true);

            var context = new ValidationContext(
                package,
                catalogIndexEntries,
                deletionAuditEntries,
                client,
                cancellationToken);

            Assert.Same(package, context.Package);
            Assert.Equal(catalogIndexEntries.Count(), context.Entries.Count);
            Assert.Equal(deletionAuditEntries.Count(), context.DeletionAuditEntries.Count);
            Assert.Same(client, context.Client);
            Assert.Equal(cancellationToken, context.CancellationToken);
        }

        [Fact]
        public void GetCachedResultAsync_ReturnsMemoizedBoolTask()
        {
            var context = new ValidationContext();

            var task1 = context.GetCachedResultAsync(_key, new Lazy<Task<bool>>(() => Task.FromResult(true)));
            var task2 = context.GetCachedResultAsync(_key, new Lazy<Task<bool>>(() => Task.FromResult(true)));

            Assert.Same(task1, task2);
        }

        [Fact]
        public void GetCachedResultAsync_ReturnsMemoizedIndexTask()
        {
            var context = new ValidationContext();

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
        public void GetMemoizedLeafAsync_ReturnsMemoizedLeafTask()
        {
            var context = new ValidationContext();

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
    }
}