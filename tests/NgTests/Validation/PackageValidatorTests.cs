// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class PackageValidatorTests
    {
        private readonly List<ValidationResult> _validationResults;
        private readonly DummyAggregateValidator _aggregateValidator;
        private readonly IEnumerable<IAggregateValidator> _aggregateValidators;
        private readonly ValidationSourceRepositories _sourceRepositories;
        private readonly TestStorageFactory _storageFactory = new TestStorageFactory();
        private readonly ILogger<PackageValidator> _logger = Mock.Of<ILogger<PackageValidator>>();
        private readonly ILogger<ValidationContext> _contextLogger = Mock.Of<ILogger<ValidationContext>>();

        public PackageValidatorTests()
        {
            _validationResults = new List<ValidationResult>();
            _aggregateValidator = new DummyAggregateValidator(_validationResults);
            _aggregateValidators = new[] { _aggregateValidator };

            _sourceRepositories = new ValidationSourceRepositories(
                Mock.Of<SourceRepository>(), 
                Mock.Of<SourceRepository>());
        }

        [Fact]
        public void Constructor_WhenAggregateValidatorsIsNull_Throws()
        {
            const IEnumerable<IAggregateValidator> aggregateValidators = null;

            var exception = Assert.Throws<ArgumentException>(
                () => new PackageValidator(
                    aggregateValidators,
                    _storageFactory,
                    _sourceRepositories,
                    _logger,
                    _contextLogger));

            Assert.Equal("aggregateValidators", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenAggregateValidatorsIsEmpty_Throws()
        {
            var aggregateValidators = Enumerable.Empty<IAggregateValidator>();

            var exception = Assert.Throws<ArgumentException>(
                () => new PackageValidator(
                    aggregateValidators,
                    _storageFactory,
                    _sourceRepositories,
                    _logger,
                    _contextLogger));

            Assert.Equal("aggregateValidators", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenAuditingStorageFactoryIsNull_Throws()
        {
            const StorageFactory storageFactory = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new PackageValidator(
                    _aggregateValidators,
                    storageFactory,
                    _sourceRepositories,
                    _logger,
                    _contextLogger));

            Assert.Equal("auditingStorageFactory", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenFeedToSourceIsNull_Throws()
        {
            const ValidationSourceRepositories sourceRepositories = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new PackageValidator(
                    _aggregateValidators,
                    _storageFactory,
                    sourceRepositories,
                    _logger,
                    _contextLogger));

            Assert.Equal("sourceRepositories", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenContextLoggerIsNull_Throws()
        {
            const ILogger<ValidationContext> contextLogger = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new PackageValidator(
                    _aggregateValidators,
                    _storageFactory,
                    _sourceRepositories,
                    _logger,
                    contextLogger));

            Assert.Equal("contextLogger", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_InitializesInstance()
        {
            var validator = new PackageValidator(
                _aggregateValidators,
                _storageFactory,
                _sourceRepositories,
                _logger,
                _contextLogger);

            Assert.Equal(_aggregateValidators, validator.AggregateValidators);
        }

        [Fact]
        public async Task ValidateAsync_WhenContextIsNull_Throws()
        {
            var validator = new PackageValidator(
                _aggregateValidators,
                _storageFactory,
                _sourceRepositories,
                _logger,
                _contextLogger);

            const PackageValidatorContext context = null;

            using (var client = new CollectorHttpClient())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => validator.ValidateAsync(context, client, CancellationToken.None));

                Assert.Equal("context", exception.ParamName);
            }
        }

        [Fact]
        public async Task ValidateAsync_WhenClientIsNull_Throws()
        {
            var validator = new PackageValidator(
                _aggregateValidators,
                _storageFactory,
                _sourceRepositories,
                _logger,
                _contextLogger);

            var context = new PackageValidatorContext(
                new FeedPackageIdentity(id: "a", version: "1.0.0"),
                catalogEntries: Enumerable.Empty<CatalogIndexEntry>());
            const CollectorHttpClient client = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => validator.ValidateAsync(context, client, CancellationToken.None));

            Assert.Equal("client", exception.ParamName);
        }

        [Fact]
        public async Task ValidateAsync_WhenCancellationTokenIsCancelled_Throws()
        {
            var validator = new PackageValidator(
                _aggregateValidators,
                _storageFactory,
                _sourceRepositories,
                _logger,
                _contextLogger);

            var context = new PackageValidatorContext(
                new FeedPackageIdentity(id: "a", version: "1.0.0"),
                catalogEntries: Enumerable.Empty<CatalogIndexEntry>());

            using (var client = new CollectorHttpClient())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => validator.ValidateAsync(context, client, new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task ValidateAsync_WhenArgumentsAreValid_ReturnsResults()
        {
            var validator = new PackageValidator(
                _aggregateValidators,
                _storageFactory,
                _sourceRepositories,
                _logger,
                _contextLogger);

            var packageIdentity = new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0"));
            var catalogEntries = new[]
            {
                new CatalogIndexEntry(
                    new Uri($"https://nuget.test/{packageIdentity.Id}"),
                    CatalogConstants.NuGetPackageDetails,
                    Guid.NewGuid().ToString(),
                    DateTime.UtcNow,
                    packageIdentity)
            };
            var context = new PackageValidatorContext(new FeedPackageIdentity(packageIdentity), catalogEntries);

            using (var httpClient = new CollectorHttpClient())
            {
                var actualResult = await validator.ValidateAsync(context, httpClient, CancellationToken.None);

                Assert.Equal(catalogEntries, actualResult.CatalogEntries);
                Assert.Empty(actualResult.DeletionAuditEntries);
                Assert.Equal(packageIdentity, actualResult.Package);

                Assert.Single(actualResult.AggregateValidationResults);

                var actualValidationResult = actualResult.AggregateValidationResults.Single();
                Assert.Same(_aggregateValidator, actualValidationResult.AggregateValidator);
                Assert.Same(_validationResults, actualValidationResult.ValidationResults);
            }
        }
    }
}