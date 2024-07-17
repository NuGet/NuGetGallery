// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using NuGetGallery;
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

        public static IEnumerable<object[]> UpdateDeletesOldStatuses_Data
        {
            get
            {
                yield return new object[] { null };
                foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                {
                    yield return new object[] { state };
                }
            }
        }

        [Theory]
        [MemberData(nameof(UpdateDeletesOldStatuses_Data))]
        public async Task UpdateDeletesOldStatuses(PackageState? previousState)
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

            var etag = "theETag";
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                if (previousState != state)
                {
                    status.ExistingState[state] = AccessConditionWrapper.GenerateIfNotExistsCondition();
                    continue;
                }

                var content = new StringStorageContentWithETag("{}", etag);
                await SaveToStorage(storageFactory, state, feedPackageIdentity, content);
                status.ExistingState[state] = AccessConditionWrapper.GenerateIfMatchCondition(etag);
            }

            // Act
            await statusService.UpdateAsync(status, CancellationToken.None);

            // Assert
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                Assert.Equal(
                    state == status.State,
                    DoesPackageExists(storageFactory, state, feedPackageIdentity));
            }

            PackageMonitoringStatusTestUtility.AssertStatus(
                status, 
                await statusService.GetAsync(feedPackageIdentity, CancellationToken.None));
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
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "json.newtonsoft", 
                    "1.0.9",
                    TestResult.Pass),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "json.newtonsoft.json", 
                    "1.0.9.1", 
                    TestResult.Fail),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "j.n.j", 
                    "1.9.1", 
                    TestResult.Skip),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "newtonsoft.json", 
                    "9.0.2",
                    TestResult.Pass)
            };

            var desiredPackageId = "newtonsoft.json";
            var desiredPackageVersion = "9.0.1";
            var desiredStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                desiredPackageId, 
                desiredPackageVersion,
                TestResult.Pass);

            await statusService.UpdateAsync(desiredStatus, CancellationToken.None);
            await Task.WhenAll(undesiredStatuses.Select(s => statusService.UpdateAsync(s, CancellationToken.None)));

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            PackageMonitoringStatusTestUtility.AssertStatus(desiredStatus, status);
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
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "json.newtonsoft", 
                    "1.0.9", 
                    TestResult.Pass),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "json.newtonsoft.json", 
                    "1.0.9.1", 
                    TestResult.Fail),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "j.n.j", 
                    "1.9.1", 
                    TestResult.Skip),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "newtonsoft.json", 
                    "9.0.2", 
                    TestResult.Pass)
            };

            var desiredPackageId = "newtonsoft.json";
            var desiredPackageVersion = "9.0.1";
            var desiredStatus = PackageMonitoringStatusTestUtility.CreateStatusWithException(desiredPackageId, desiredPackageVersion);

            await statusService.UpdateAsync(desiredStatus, CancellationToken.None);
            await Task.WhenAll(undesiredStatuses.Select(s => statusService.UpdateAsync(s, CancellationToken.None)));

            // Act
            var status = await statusService.GetAsync(new FeedPackageIdentity(desiredPackageId, desiredPackageVersion), CancellationToken.None);

            // Assert
            PackageMonitoringStatusTestUtility.AssertStatus(desiredStatus, status);
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
                storage.ResolveUri(GetPackageFileName(desiredPackageId, desiredPackageVersion)),
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

        public static IEnumerable<object[]> GetByPackageDeletesOutdatedStatuses_Data
        {
            get
            {
                foreach (var latest in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                {
                    foreach (var outdated in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                    {
                        yield return new object[] { latest, outdated };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetByPackageDeletesOutdatedStatuses_Data))]
        public async Task GetByPackageDeletesOutdatedStatuses(PackageState latest, PackageState outdated)
        {
            // Arrange
            var storageFactory = new MemoryStorageFactory();
            var statusService = new PackageMonitoringStatusService(
                storageFactory,
                new Mock<ILogger<PackageMonitoringStatusService>>().Object);

            var id = "howdyFriend";
            var version = "5.5.5";
            var package = new FeedPackageIdentity(id, version);
            var outdatedStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                id, 
                version, 
                PackageMonitoringStatusTestUtility.GetTestResultFromPackageState(latest),
                new DateTime(2019, 6, 10));

            var latestStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                id,
                version,
                PackageMonitoringStatusTestUtility.GetTestResultFromPackageState(outdated),
                new DateTime(2019, 6, 11));

            await SaveToStorage(storageFactory, outdatedStatus);
            await SaveToStorage(storageFactory, latestStatus);

            // Act
            var status = await statusService.GetAsync(package, CancellationToken.None);

            // Assert
            PackageMonitoringStatusTestUtility.AssertStatus(latestStatus, status);
            Assert.Equal(latest == outdated, DoesPackageExists(storageFactory, outdatedStatus.State, package));
            Assert.True(DoesPackageExists(storageFactory, latestStatus.State, package));
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
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "newtonsoft.json", "9.0.2", TestResult.Pass),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "a.b", "1.2.3", TestResult.Pass),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "newtonsoft.json", "6.0.8", TestResult.Skip),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "a.b", "0.8.9", TestResult.Skip)
            };

            var expectedInvalidStatuses = new PackageMonitoringStatus[]
            {
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "jQuery", 
                    "3.1.2", 
                    new ValidationResult[] 
                    {
                        PackageMonitoringStatusTestUtility.CreateValidationResult(
                            TestResult.Fail, 
                            new ValidationException("malarky!"))
                    }),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "EntityFramework", 
                    "6.1.2", 
                    new ValidationResult[] 
                    {
                        PackageMonitoringStatusTestUtility.CreateValidationResult(
                            TestResult.Fail, 
                            new ValidationException("absurd!"))
                    }),
                PackageMonitoringStatusTestUtility.CreateStatusWithException(
                    "NUnit", 
                    "3.6.1")
            };

            var expectedUnknownStatuses = new PackageMonitoringStatus[]
            {
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "xunit", "2.4.1", TestResult.Pending),
                PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                    "a.b", "99.9.99", TestResult.Pending)
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
            PackageMonitoringStatusTestUtility.AssertAll(
                expectedValidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                validStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                PackageMonitoringStatusTestUtility.AssertStatus);

            PackageMonitoringStatusTestUtility.AssertAll(
                expectedInvalidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                invalidStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                PackageMonitoringStatusTestUtility.AssertStatus);

            PackageMonitoringStatusTestUtility.AssertAll(
                expectedUnknownStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                unknownStatuses.OrderBy(s => s.Package.Id).ThenBy(s => s.Package.Version),
                PackageMonitoringStatusTestUtility.AssertStatus);
        }

        private Task SaveToStorage(MemoryStorageFactory storageFactory, PackageMonitoringStatus status)
        {
            var json = JsonConvert.SerializeObject(status, JsonSerializerUtility.SerializerSettings);
            var content = new StringStorageContentWithAccessCondition(
                json,
                AccessConditionWrapper.GenerateEmptyCondition(),
                "application/json");

            return SaveToStorage(storageFactory, status.State, status.Package, content);
        }

        private Task SaveToStorage(MemoryStorageFactory storageFactory, PackageState state, FeedPackageIdentity package, StorageContent content)
        {
            var stateName = Enum.GetName(typeof(PackageState), state);
            var storage = storageFactory.Create(stateName.ToLowerInvariant());
            var packageFileName = GetPackageFileName(package.Id, package.Version);
            return storage.SaveAsync(storage.ResolveUri(packageFileName), content, CancellationToken.None);
        }

        private bool DoesPackageExists(MemoryStorageFactory storageFactory, PackageState state, FeedPackageIdentity package)
        {
            var stateName = Enum.GetName(typeof(PackageState), state);
            var storage = storageFactory.Create(stateName.ToLowerInvariant());
            var packageFileName = GetPackageFileName(package.Id, package.Version);
            return storage.Exists(packageFileName);
        }
    }
}