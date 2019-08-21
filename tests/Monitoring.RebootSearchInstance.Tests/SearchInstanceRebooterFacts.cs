// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Services.AzureManagement;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class SearchInstanceRebooterFacts
    {
        private const string _region = "USSC";
        private const string _slot = "Production";
        private const string _resourceGroup = "test-rg";
        private const string _serviceName = "test-search-0";
        private const string _role = "SearchService";
        private const string _subscription = "TEST";
        private readonly ITestOutputHelper _output;
        private readonly Mock<IFeedClient> _feedClient;
        private readonly Mock<ISearchServiceClient> _searchServiceClient;
        private readonly Mock<IAzureManagementAPIWrapper> _azureManagementAPIWrapper;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly Mock<IOptionsSnapshot<MonitorConfiguration>> _configurationMock;
        private readonly MonitorConfiguration _configuration;
        private readonly ILogger<SearchInstanceRebooter> _logger;
        private readonly CancellationToken _token;
        private readonly List<Instance> _instances;
        private readonly DateTimeOffset _feedTimestamp;
        private readonly SearchInstanceRebooter _target;

        public SearchInstanceRebooterFacts(ITestOutputHelper output)
        {
            _output = output;
            _feedClient = new Mock<IFeedClient>();
            _searchServiceClient = new Mock<ISearchServiceClient>();
            _azureManagementAPIWrapper = new Mock<IAzureManagementAPIWrapper>();
            _telemetryService = new Mock<ITelemetryService>();
            _configurationMock = new Mock<IOptionsSnapshot<MonitorConfiguration>>();
            _configuration = new MonitorConfiguration
            {
                ProcessLifetime = TimeSpan.Zero,
                SleepDuration = TimeSpan.Zero,
                HealthyThresholdInSeconds = 60,
                UnhealthyThresholdInSeconds = 120,
                Role = _role,
                Subscription = _subscription,
                RoleInstanceFormat = "RoleInstance_{0}",
                RegionInformations = new List<RegionInformation>
                {
                    new RegionInformation
                    {
                        Region = _region,
                        ResourceGroup = _resourceGroup,
                        ServiceName = _serviceName,
                    },
                },
            };
            _logger = new LoggerFactory()
                .AddXunit(_output)
                .CreateLogger<SearchInstanceRebooter>();

            _token = CancellationToken.None;
            _instances = new List<Instance>
            {
                new Instance(
                    _slot,
                    0,
                    "http://localhost:801/search/diag",
                    "http://localhost:801/query",
                    _region,
                    ServiceType.LuceneSearch),
                new Instance(
                    _slot,
                    1,
                    "http://localhost:802/search/diag",
                    "http://localhost:802/query",
                    _region,
                    ServiceType.LuceneSearch),
                new Instance(
                    _slot,
                    2,
                    "http://localhost:803/search/diag",
                    "http://localhost:803/query",
                    _region,
                    ServiceType.LuceneSearch),
            };
            _feedTimestamp = new DateTimeOffset(2018, 1, 1, 8, 0, 0, TimeSpan.Zero);

            _configurationMock
                .Setup(x => x.Value)
                .Returns(() => _configuration);
            _searchServiceClient
                .Setup(x => x.GetSearchEndpointsAsync(It.IsAny<RegionInformation>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _instances);
            _feedClient
                .Setup(x => x.GetLatestFeedTimeStampAsync())
                .ReturnsAsync(() => _feedTimestamp);

            _target = new SearchInstanceRebooter(
                _feedClient.Object,
                _searchServiceClient.Object,
                _azureManagementAPIWrapper.Object,
                _telemetryService.Object,
                _configurationMock.Object,
                _logger);
        }

        [Fact]
        public async Task RestartsFirstUnhealthyInstance()
        {
            _searchServiceClient
                .Setup(x => x.GetCommitDateTimeAsync(It.Is<Instance>(i => i.Index == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.MaxValue);
            _searchServiceClient
                .SetupSequence(x => x.GetCommitDateTimeAsync(It.Is<Instance>(i => i.Index == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.MinValue)
                .ThrowsAsync(new IOException("The instance is not up yet"));
            _searchServiceClient
                .Setup(x => x.GetCommitDateTimeAsync(It.Is<Instance>(i => i.Index == 2), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.MinValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    _subscription,
                    _resourceGroup,
                    _serviceName,
                    "Production",
                    _role,
                    "RoleInstance_1",
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 1), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 2), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);

            _telemetryService.Verify(x => x.TrackInstanceReboot(_region, 1), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceReboot(It.IsAny<string>(), It.IsAny<int>()), Times.Once);

            _telemetryService.Verify(
                x => x.TrackInstanceRebootDuration(_region, 1, It.IsAny<TimeSpan>(), InstanceHealth.Unknown),
                Times.Once);
            _telemetryService.Verify(
                x => x.TrackInstanceRebootDuration(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<InstanceHealth>()),
                Times.Once);
        }

        [Fact]
        public async Task TreatsUnknownExceptionWhenGettingCommitTimestampAsUnknown()
        {
            _searchServiceClient
                .SetupSequence(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Some problem with the network."))
                .ReturnsAsync(DateTimeOffset.MaxValue)
                .ReturnsAsync(DateTimeOffset.MaxValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 2), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 1), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task TreatsUnknownHttpStatusCodeExceptionWhenGettingCommitTimestampAsUnknown(HttpStatusCode statusCode)
        {
            _searchServiceClient
                .SetupSequence(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpResponseException(statusCode, "Service Unavailable", "Some problem."))
                .ReturnsAsync(DateTimeOffset.MaxValue)
                .ReturnsAsync(DateTimeOffset.MaxValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 2), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 1), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task TreatsSome500sHttpResponseExceptionAsUnhealthy(HttpStatusCode statusCode)
        {
            _searchServiceClient
                .SetupSequence(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpResponseException(statusCode, "Service Unavailable", "Some problem."))
                .ReturnsAsync(DateTimeOffset.MaxValue)
                .ReturnsAsync(DateTimeOffset.MaxValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    _subscription,
                    _resourceGroup,
                    _serviceName,
                    "Production",
                    _role,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 2), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 1), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }

        [Fact]
        public async Task TreatsLagBetweenThresholdsAsUnknown()
        {
            _searchServiceClient
                .Setup(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_feedTimestamp.Subtract(TimeSpan.FromSeconds(_configuration.UnhealthyThresholdInSeconds - 1)));

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 3), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }

        [Fact]
        public async Task DoesNothingWhenThereAreNoUnhealthyInstances()
        {
            _searchServiceClient
                .Setup(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => DateTimeOffset.MaxValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 3), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }

        [Fact]
        public async Task DoesNothingWhenThereAreNoHealthyInstances()
        {
            _searchServiceClient
                .Setup(x => x.GetCommitDateTimeAsync(It.IsAny<Instance>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => DateTimeOffset.MinValue);

            await _target.RunAsync(_token);

            _azureManagementAPIWrapper.Verify(
                x => x.RebootCloudServiceRoleInstanceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _telemetryService.Verify(x => x.TrackHealthyInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackUnhealthyInstanceCount(_region, 3), Times.Once);
            _telemetryService.Verify(x => x.TrackUnknownInstanceCount(_region, 0), Times.Once);
            _telemetryService.Verify(x => x.TrackInstanceCount(_region, 3), Times.Once);
        }
    }
}
