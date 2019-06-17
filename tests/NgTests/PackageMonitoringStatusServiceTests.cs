// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using Xunit;

namespace NgTests
{
    public class PackageMonitoringStatusServiceTests
    {
        public IPackageMonitoringStatusService Service { get; }

        public PackageMonitoringStatusServiceTests()
        {
            Service = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);
        }

        private string GetPackageFileName(string packageId, string packageVersion)
        {
            return $"{packageId.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.{packageVersion.ToLowerInvariant()}.json";
        }

        [Fact]
        public async Task UpdateSavesNewStatus()
        {
            // Arrange
            var feedPackageIdentity = new FeedPackageIdentity("howdy", "3.4.6");
            var packageValidationResult = new PackageValidationResult(
                new PackageIdentity(feedPackageIdentity.Id, new NuGetVersion(feedPackageIdentity.Version)),
                null,
                null,
                Enumerable.Empty<AggregateValidationResult>());

            var status = new PackageMonitoringStatus(packageValidationResult);

            var storageFactory = new MemoryStorageFactory();

            var statusService = new PackageMonitoringStatusService(
                storageFactory,
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            // Act
            await statusService.UpdateAsync(status, CancellationToken.None);

            // Assert
            Assert.True(
                storageFactory.Create(
                    PackageState.Valid.ToString().ToLowerInvariant())
                .Exists(GetPackageFileName(feedPackageIdentity.Id, feedPackageIdentity.Version)));
        }

        [Fact]
        public async Task UpdateDeletesOldStatuses()
        {
            // Arrange
            var feedPackageIdentity = new FeedPackageIdentity("howdy", "3.4.6");
            var packageFileName = GetPackageFileName(feedPackageIdentity.Id, feedPackageIdentity.Version);

            var packageValidationResult = new PackageValidationResult(
                new PackageIdentity(feedPackageIdentity.Id, new NuGetVersion(feedPackageIdentity.Version)),
                null,
                null,
                Enumerable.Empty<AggregateValidationResult>());
            var status = new PackageMonitoringStatus(packageValidationResult);

            var storageFactory = new MemoryStorageFactory();

            var statusService = new PackageMonitoringStatusService(
                storageFactory,
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            foreach (var state in Enum.GetNames(typeof(PackageState)))
            {
                var storage = storageFactory.Create(state.ToLowerInvariant());
                await storage.SaveAsync(storage.ResolveUri(packageFileName), new StringStorageContent("{}"), CancellationToken.None);
                Assert.True(storage.Exists(packageFileName));
            }

            // Act
            await statusService.UpdateAsync(status, CancellationToken.None);

            // Assert
            foreach (var state in Enum.GetNames(typeof(PackageState)))
            {
                var storage = storageFactory.Create(state.ToLowerInvariant());

                if ((PackageState)Enum.Parse(typeof(PackageState), state) == status.State)
                {
                    Assert.True(storage.Exists(packageFileName));
                }
                else
                {
                    Assert.False(storage.Exists(packageFileName));
                }
            }

            AssertStatus(status, await statusService.GetAsync(feedPackageIdentity, CancellationToken.None));
        }

        private static ValidationResult CreateValidationResult(TestResult result, Exception e)
        {
            return new DummyValidator(result, e).Validate();
        }

        private static CatalogIndexEntry CreateCatalogIndexEntry(string id, string version)
        {
            return new CatalogIndexEntry(
                new UriBuilder() { Path = $"{id.ToLowerInvariant()}/{id.ToLowerInvariant()}.{version.ToLowerInvariant()}" }.Uri,
                CatalogConstants.NuGetPackageDetails,
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                new PackageIdentity(id, new NuGetVersion(version)));
        }

        private static DeletionAuditEntry CreateDeletionAuditEntry(string id, string version)
        {
            return new DeletionAuditEntry(
                new UriBuilder() { Path = $"auditing/{id}/{version}/{Guid.NewGuid().ToString()}{DeletionAuditEntry.FileNameSuffixes[0]}" }.Uri,
                JObject.Parse("{\"help\":\"i'm trapped in a json factory!\"}"),
                id,
                version,
                DateTime.UtcNow);
        }

        private static PackageMonitoringStatus CreateStatusWithPackageValidationResult(string packageId, string packageVersion, IEnumerable<ValidationResult> results)
        {
            var version = new NuGetVersion(packageVersion);

            var aggregateValidationResult = new DummyAggregateValidator(results).Validate();

            var packageValidationResult = new PackageValidationResult(
                new PackageIdentity(packageId, version),
                new CatalogIndexEntry[] {
                        CreateCatalogIndexEntry(packageId, packageVersion),
                        CreateCatalogIndexEntry(packageId, packageVersion),
                        CreateCatalogIndexEntry(packageId, packageVersion)
                    },
                new DeletionAuditEntry[] {
                        CreateDeletionAuditEntry(packageId, packageVersion),
                        CreateDeletionAuditEntry(packageId, packageVersion),
                        CreateDeletionAuditEntry(packageId, packageVersion)
                    },
                new AggregateValidationResult[] { aggregateValidationResult });

            return new PackageMonitoringStatus(packageValidationResult);
        }

        private static PackageMonitoringStatus CreateStatusWithException(string packageId, string packageVersion)
        {
            return new PackageMonitoringStatus(new FeedPackageIdentity(packageId, packageVersion), new Exception());
        }

        private static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor)
        {
            Assert.Equal(accessor(expected), accessor(actual));
        }

        private static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor,
            Action<TField, TField> assert)
        {
            assert(accessor(expected), accessor(actual));
        }

        private static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, IEnumerable<TField>> accessor,
            Action<TField, TField> assert)
        {
            AssertAll(accessor(expected), accessor(actual), assert);
        }

        private static void AssertStatus(PackageMonitoringStatus expected, PackageMonitoringStatus actual)
        {
            AssertFieldEqual(expected, actual, i => i.Package.Id);
            AssertFieldEqual(expected, actual, i => i.Package.Version);
            AssertFieldEqual(expected, actual, i => i.State);

            AssertFieldEqual(expected, actual, i => i.ValidationResult, AssertPackageValidationResult);
            AssertFieldEqual(expected, actual, i => i.ValidationException, AssertException);
        }

        private static void AssertException(Exception expected, Exception actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Message);
            AssertFieldEqual(expected, actual, i => i.StackTrace);
            AssertFieldEqual(expected, actual, i => i.Data, AssertDictionary);
            AssertFieldEqual(expected, actual, i => i.InnerException, AssertException);
        }

        private static void AssertDictionary(IDictionary expected, IDictionary actual)
        {
            foreach (var expectedKey in expected.Keys)
            {
                Assert.True(actual.Contains(expectedKey));
                Assert.Equal(expected[expectedKey], actual[expectedKey]);
            }
        }

        private static void AssertPackageValidationResult(PackageValidationResult expected, PackageValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Package.Id);
            AssertFieldEqual(expected, actual, i => i.Package.Version);

            AssertFieldEqual(expected, actual, i => i.CatalogEntries, AssertCatalogIndexEntry);
            AssertFieldEqual(expected, actual, i => i.DeletionAuditEntries, AssertDeletionAuditEntry);

            AssertFieldEqual(expected, actual, i => i.AggregateValidationResults, AssertAggregateValidationResult);
        }

        private static void AssertCatalogIndexEntry(CatalogIndexEntry expected, CatalogIndexEntry actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Uri);
            AssertFieldEqual(expected, actual, i => i.Types);
            AssertFieldEqual(expected, actual, i => i.Id);
            AssertFieldEqual(expected, actual, i => i.Version);
            AssertFieldEqual(expected, actual, i => i.CommitId);
            AssertFieldEqual(expected, actual, i => i.CommitTimeStamp);
        }

        private static void AssertDeletionAuditEntry(DeletionAuditEntry expected, DeletionAuditEntry actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.PackageId);
            AssertFieldEqual(expected, actual, i => i.PackageVersion);
            AssertFieldEqual(expected, actual, i => i.Record);
            AssertFieldEqual(expected, actual, i => i.TimestampUtc);
            AssertFieldEqual(expected, actual, i => i.Uri);
        }

        private static void AssertAggregateValidationResult(AggregateValidationResult expected, AggregateValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.AggregateValidator.Name);
            AssertFieldEqual(expected, actual, i => i.ValidationResults, AssertValidationResult);
        }

        private static void AssertValidationResult(ValidationResult expected, ValidationResult actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);

                return;
            }

            AssertFieldEqual(expected, actual, i => i.Validator.Name);
            AssertFieldEqual(expected, actual, i => i.Result);

            AssertFieldEqual(expected, actual, i => i.Exception, AssertException);
        }

        [Fact]
        public async Task GetByPackageNoResults()
        {
            // Arrange
            var statusService = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            var desiredPackageId = "missingpackage";
            var desiredPackageVersion = "9.1.1";

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            Assert.Null(status);
        }

        [Fact]
        public async Task GetByPackageWithPackageValidationResult()
        {
            // Arrange
            var statusService = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            var undesiredStatuses = new PackageMonitoringStatus[]
            {
                CreateStatusWithPackageValidationResult("json.newtonsoft", "1.0.9", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) }),
                CreateStatusWithPackageValidationResult("json.newtonsoft.json", "1.0.9.1", new ValidationResult[] { CreateValidationResult(TestResult.Fail, null) }),
                CreateStatusWithPackageValidationResult("j.n.j", "1.9.1", new ValidationResult[] { CreateValidationResult(TestResult.Skip, null) }),
                CreateStatusWithPackageValidationResult("newtonsoft.json", "9.0.2", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) })
            };

            var desiredPackageId = "newtonsoft.json";
            var desiredPackageVersion = "9.0.1";
            var desiredStatus = CreateStatusWithPackageValidationResult(desiredPackageId, desiredPackageVersion, new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) });

            await statusService.UpdateAsync(desiredStatus, CancellationToken.None);
            await Task.WhenAll(undesiredStatuses.Select(s => statusService.UpdateAsync(s, CancellationToken.None)));

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            AssertStatus(desiredStatus, status);
        }

        [Fact]
        public async Task GetByPackageWithException()
        {
            // Arrange
            var statusService = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            var undesiredStatuses = new PackageMonitoringStatus[]
            {
                CreateStatusWithPackageValidationResult("json.newtonsoft", "1.0.9", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) }),
                CreateStatusWithPackageValidationResult("json.newtonsoft.json", "1.0.9.1", new ValidationResult[] { CreateValidationResult(TestResult.Fail, null) }),
                CreateStatusWithPackageValidationResult("j.n.j", "1.9.1", new ValidationResult[] { CreateValidationResult(TestResult.Skip, null) }),
                CreateStatusWithPackageValidationResult("newtonsoft.json", "9.0.2", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) })
            };

            var desiredPackageId = "newtonsoft.json";
            var desiredPackageVersion = "9.0.1";
            var desiredStatus = CreateStatusWithException(desiredPackageId, desiredPackageVersion);

            await statusService.UpdateAsync(desiredStatus, CancellationToken.None);
            await Task.WhenAll(undesiredStatuses.Select(s => statusService.UpdateAsync(s, CancellationToken.None)));

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            AssertStatus(desiredStatus, status);
        }

        [Fact]
        public async Task GetByPackageDeserializationException()
        {
            // Arrange
            var desiredPackageId = "brokenpackage";
            var desiredPackageVersion = "99.9.99";

            var storageFactory = new MemoryStorageFactory();
            var storage = storageFactory.Create(PackageState.Valid.ToString().ToLowerInvariant());

            await storage.SaveAsync(
                storage.ResolveUri($"{desiredPackageId}/{desiredPackageId}.{desiredPackageVersion}.json"),
                new StringStorageContent("this isn't json"),
                CancellationToken.None);

            var statusService = new PackageMonitoringStatusService(
                storageFactory,
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            Assert.Equal(desiredPackageId, status.Package.Id);
            Assert.Equal(desiredPackageVersion, status.Package.Version);
            Assert.IsType<StatusDeserializationException>(status.ValidationException);
        }

        private static void AssertAll<T>(IEnumerable<T> expecteds, IEnumerable<T> actuals, Action<T, T> assert)
        {
            if (expecteds == null)
            {
                Assert.Null(actuals);

                return;
            }

            Assert.Equal(expecteds.Count(), actuals.Count());
            var expectedsArray = expecteds.ToArray();
            var actualsArray = actuals.ToArray();
            for (int i = 0; i < expecteds.Count(); i++)
            {
                assert(expectedsArray[i], actualsArray[i]);
            }
        }

        [Fact]
        public async Task GetByStateNoResults()
        {
            // Arrange
            var statusService = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            // Act & Assert
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                var statuses = await statusService.GetAsync(state, CancellationToken.None);
                Assert.Empty(statuses);
            }
        }

        [Fact]
        public async Task GetByState()
        {
            // Arrange
            var statusService = new PackageMonitoringStatusService(
                new MemoryStorageFactory(),
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            var expectedValidStatuses = new PackageMonitoringStatus[]
            {
                CreateStatusWithPackageValidationResult("newtonsoft.json", "9.0.2", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) }),
                CreateStatusWithPackageValidationResult("a.b", "1.2.3", new ValidationResult[] { CreateValidationResult(TestResult.Pass, null) }),
                CreateStatusWithPackageValidationResult("newtonsoft.json", "6.0.8", new ValidationResult[] { CreateValidationResult(TestResult.Skip, null) }),
                CreateStatusWithPackageValidationResult("a.b", "0.8.9", new ValidationResult[] { CreateValidationResult(TestResult.Skip, null) })
            };

            var expectedInvalidStatuses = new PackageMonitoringStatus[]
            {
                CreateStatusWithPackageValidationResult("jQuery", "3.1.2", new ValidationResult[] { CreateValidationResult(TestResult.Fail, new ValidationException("malarky!")) }),
                CreateStatusWithPackageValidationResult("EntityFramework", "6.1.2", new ValidationResult[] { CreateValidationResult(TestResult.Fail, new ValidationException("absurd!")) }),
                CreateStatusWithException("NUnit", "3.6.1")
            };

            var expectedUnknownStatuses = new PackageMonitoringStatus[]
            {
                CreateStatusWithPackageValidationResult("xunit", "2.4.1", new ValidationResult[] { CreateValidationResult(TestResult.Pending, null) }),
                CreateStatusWithPackageValidationResult("a.b", "99.9.99", new ValidationResult[] { CreateValidationResult(TestResult.Pending, null) })
            };

            foreach (var expectedValidStatus in expectedValidStatuses)
            {
                await statusService.UpdateAsync(expectedValidStatus, CancellationToken.None);
            }

            foreach (var expectedInvalidStatus in expectedInvalidStatuses)
            {
                await statusService.UpdateAsync(expectedInvalidStatus, CancellationToken.None);
            }

            foreach (var expectedSkippedStatus in expectedUnknownStatuses)
            {
                await statusService.UpdateAsync(expectedSkippedStatus, CancellationToken.None);
            }

            // Act
            var validStatuses = await statusService.GetAsync(PackageState.Valid, CancellationToken.None);
            var invalidStatuses = await statusService.GetAsync(PackageState.Invalid, CancellationToken.None);
            var unknownStatuses = await statusService.GetAsync(PackageState.Unknown, CancellationToken.None);

            // Assert
            AssertAll(
                expectedValidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                validStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                AssertStatus);

            AssertAll(
                expectedInvalidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                invalidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                AssertStatus);

            AssertAll(
                expectedUnknownStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                unknownStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                AssertStatus);
        }
    }
}