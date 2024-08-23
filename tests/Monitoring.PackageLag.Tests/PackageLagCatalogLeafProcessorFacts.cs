﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Jobs.Monitoring.PackageLag.Telemetry;
using NuGet.Protocol.Catalog;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Monitoring.PackageLag.Tests
{
    public class PackageLagCatalogLeafProcessorFacts
    {
        private const string _region = "USSC";
        private const string _slot = "Production";
        private const string _resourceGroup = "test-rg";
        private const string _serviceName = "test-search-0";
        private readonly ITestOutputHelper _output;
        private readonly Mock<ISearchServiceClient> _searchServiceClient;
        private readonly Mock<IPackageLagTelemetryService> _telemetryService;
        private readonly Mock<IOptionsSnapshot<PackageLagMonitorConfiguration>> _configurationMock;
        private readonly PackageLagMonitorConfiguration _configuration;
        private readonly ILogger<PackageLagCatalogLeafProcessor> _logger;
        private readonly List<Instance> _instances;
        private readonly PackageLagCatalogLeafProcessor _target;

        public PackageLagCatalogLeafProcessorFacts(ITestOutputHelper output)
        {
            _output = output;
            _searchServiceClient = new Mock<ISearchServiceClient>();;
            _telemetryService = new Mock<IPackageLagTelemetryService>();
            _configurationMock = new Mock<IOptionsSnapshot<PackageLagMonitorConfiguration>>();
            _configuration = new PackageLagMonitorConfiguration
            {
                ServiceIndexUrl = "http://localhost:801/search/diag",
                RegionInformations = new List<RegionInformation>
                {
                    new RegionInformation
                    {
                        Region = _region,
                        ResourceGroup = _resourceGroup,
                        ServiceName = _serviceName,
                    },
                }
            };

            _logger = new LoggerFactory()
                .AddXunit(_output)
                .CreateLogger<PackageLagCatalogLeafProcessor>();

            _instances = new List<Instance>
            {
                new Instance(
                    _slot,
                    0,
                    "http://localhost:801/search/diag",
                    "http://localhost:801/query",
                    _region,
                    ServiceType.AzureSearch),
                new Instance(
                    _slot,
                    1,
                    "http://localhost:802/search/diag",
                    "http://localhost:802/query",
                    _region,
                    ServiceType.AzureSearch)
            };

            _configurationMock
                .Setup(x => x.Value)
                .Returns(() => _configuration);
            _searchServiceClient
                .Setup(x => x.GetSearchEndpoints(It.IsAny<RegionInformation>()))
                .Returns(() => _instances);

            var regionInformations = _configuration.RegionInformations;
            var instances = new List<Instance>();

            foreach (var regionInformation in regionInformations)
            {
                instances.AddRange(_searchServiceClient.Object.GetSearchEndpoints(regionInformation));
            }

            _target = new PackageLagCatalogLeafProcessor(instances, _searchServiceClient.Object, _telemetryService.Object, _logger);
            _target.WaitBetweenPolls = TimeSpan.FromSeconds(5);
        }

        [Fact]
        public async Task ListOperationDoesNotLogCreationLag()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetTestSearchResponse(currentTime, currentTime - TimeSpan.FromSeconds(200), currentTime - TimeSpan.FromSeconds(200), false);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, newTime, newTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(newTime));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            var success = await _target.ProcessPackageDetailsAsync(listPackageLeaf);
            Assert.True(await _target.WaitForProcessing());
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PushOperationLogsAll()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetEmptyTestSearchResponse(currentTime);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, currentTime, currentTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(newTime));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Verifiable();

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Verifiable();

            var success = await _target.ProcessPackageDetailsAsync(listPackageLeaf);
            Assert.True(await _target.WaitForProcessing());
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            _telemetryService.Verify();
        }

        [Fact]
        public async Task PushOperationLogsCorrectLag()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime + TimeSpan.FromSeconds(50),
                LastEdited = currentTime + TimeSpan.FromSeconds(100),
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetEmptyTestSearchResponse(currentTime);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newCreatedTime = listPackageLeaf.Created;
            var newLastEditedTime = listPackageLeaf.LastEdited;
            var expectedLag = newTime - listPackageLeaf.Created;
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, newCreatedTime, newLastEditedTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(newTime));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Verifiable();

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Verifiable();

            var lag = await _target.ProcessPackageLagDetailsAsync(listPackageLeaf, listPackageLeaf.Created, listPackageLeaf.LastEdited, expectListed: true, isDelete: false);
            Assert.True(await _target.WaitForProcessing());
            Assert.Equal(expectedLag, lag);
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            _telemetryService.Verify();
        }

        [Fact]
        public async Task ListOperationLogsCorrectLag()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime + TimeSpan.FromSeconds(50),
                LastEdited = currentTime + TimeSpan.FromSeconds(100),
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetTestSearchResponse(currentTime, currentTime, currentTime, listed: false);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newCreatedTime = newTime - TimeSpan.FromSeconds(50);
            var newLastEditedTime = listPackageLeaf.LastEdited;
            var expectedLag = newTime - listPackageLeaf.LastEdited;
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, newCreatedTime, newLastEditedTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(newTime));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Verifiable();

            var lag = await _target.ProcessPackageLagDetailsAsync(listPackageLeaf, listPackageLeaf.Created, listPackageLeaf.LastEdited, expectListed: true, isDelete: false);
            Assert.Equal(expectedLag, lag);
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _searchServiceClient.Verify(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            _telemetryService.Verify();
        }

        [Fact]
        public async Task QueryAbandonedIfRetryLimitReached()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetEmptyTestSearchResponse(currentTime);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, currentTime, currentTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(oldSearchResponse))
                .Returns(Task.FromResult(newSearchResponse));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Unexpected call to get reload time"));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _target.RetryLimit = 2;

            var lag = await _target.ProcessPackageLagDetailsAsync(listPackageLeaf, listPackageLeaf.Created, listPackageLeaf.LastEdited, expectListed: true, isDelete: false);
            Assert.Null(lag);
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("801")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(_target.RetryLimit));
            _searchServiceClient.Verify(ssc => ssc.GetResultForPackageIdVersion(It.Is<Instance>(i => i.DiagUrl.Contains("802")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(_target.RetryLimit));

            _telemetryService.Verify();
        }

        [Fact]
        public async Task NoInstancesDoesNotLog()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var emptyInstances = new List<Instance>();
            var newTarget = new PackageLagCatalogLeafProcessor(emptyInstances, _searchServiceClient.Object, _telemetryService.Object, _logger);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Unexpectd call to get search result"));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Unexpected call to get reload time"));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            var lag = await _target.ProcessPackageLagDetailsAsync(listPackageLeaf, listPackageLeaf.Created, listPackageLeaf.LastEdited, expectListed: true, isDelete: false);
            Assert.Null(lag);
        }

        [Fact]
        public async Task LagComputeReturnsNullWhenExceptionIsThrown()
        {
            var currentTime = DateTimeOffset.UtcNow;
            PackageDetailsCatalogLeaf listPackageLeaf = new PackageDetailsCatalogLeaf
            {
                PackageId = "Test",
                PackageVersion = "1.0.0",
                Created = currentTime,
                LastEdited = currentTime,
                Listed = true
            };

            var oldSearchResponse = TestHelpers.GetEmptyTestSearchResponse(currentTime);

            var newTime = currentTime + TimeSpan.FromSeconds(200);
            var newSearchResponse = TestHelpers.GetTestSearchResponse(newTime, currentTime, currentTime);

            _searchServiceClient.SetupSequence(ssc => ssc.GetResultForPackageIdVersion(It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Emulating Failure"));

            _searchServiceClient.Setup(ssc => ssc.GetIndexLastReloadTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Unexpected call to get reload time"));

            _telemetryService
                .Setup(ts => ts.TrackPackageCreationLag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _telemetryService
                .Setup(ts => ts.TrackV3Lag(It.IsAny<DateTimeOffset>(), It.IsAny<Instance>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Throws(new Exception("Unexpected Logging"));

            _target.RetryLimit = 2;

            var lag = await _target.ProcessPackageLagDetailsAsync(listPackageLeaf, listPackageLeaf.Created, listPackageLeaf.LastEdited, expectListed: true, isDelete: false);
            Assert.Null(lag);
        }
    }
}
