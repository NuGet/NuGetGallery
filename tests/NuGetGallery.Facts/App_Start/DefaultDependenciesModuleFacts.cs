// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.Web;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Moq;
using NuGet.Services.Logging;
using NuGet.Services.Sql;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class DefaultDependenciesModuleFacts
    {
        public class TheConfigureApplicationInsightsMethod
        {
            private readonly IAppConfiguration _appConfiguration;

            public TheConfigureApplicationInsightsMethod()
            {
                _appConfiguration = new AppConfiguration();

                // Configure Application Insights dummy default values
                _appConfiguration.AppInsightsHeartbeatIntervalSeconds = 60;
                _appConfiguration.AppInsightsSamplingPercentage = 50;
                _appConfiguration.AppInsightsInstrumentationKey = Guid.NewGuid().ToString();
            }

            [Fact]
            public void ConfiguresHeartbeatInterval()
            {
                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(_appConfiguration, out var _);

                // Assert
                Assert.Equal(
                    _appConfiguration.AppInsightsInstrumentationKey,
                    aiConfiguration.TelemetryConfiguration.InstrumentationKey);

                Assert.Equal(
                    _appConfiguration.AppInsightsHeartbeatIntervalSeconds,
                    aiConfiguration.DiagnosticsTelemetryModule.HeartbeatInterval.TotalSeconds);
            }

            [Fact]
            public void ConfiguresInstrumentationKey()
            {
                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(_appConfiguration, out var _);

                // Assert
                Assert.Equal(
                    _appConfiguration.AppInsightsInstrumentationKey,
                    aiConfiguration.TelemetryConfiguration.InstrumentationKey);
            }

            [Fact]
            public void ConfiguresTelemetryClientWrapper()
            {
                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(
                    _appConfiguration,
                    out var telemetryClient);

                // Assert
                Assert.NotNull(telemetryClient);
                var telemetryClientWrapper = Assert.IsType<TelemetryClientWrapper>(telemetryClient);

                Assert.Equal(
                    _appConfiguration.AppInsightsInstrumentationKey,
                    telemetryClientWrapper.UnderlyingClient.InstrumentationKey);

                Assert.Equal(
                    _appConfiguration.AppInsightsInstrumentationKey,
                    telemetryClientWrapper.UnderlyingClient.Context.InstrumentationKey);
            }

            [Theory]
            [InlineData("deployment-label")]
            [InlineData(null)]
            public void ConfiguresTelemetryInitializers(string deploymentLabel)
            {
                // Arrange
                _appConfiguration.DeploymentLabel = deploymentLabel;
                var elementInspectors = GetTelemetryInitializerInspectors(deploymentLabel, deploymentId: null);

                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(
                    _appConfiguration,
                    out var _);

                // Assert
                Assert.Collection(
                    aiConfiguration.TelemetryConfiguration.TelemetryInitializers,
                    elementInspectors);
            }

            [Fact]
            public void SetsApplicationIdProvider()
            {
                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(
                    _appConfiguration,
                    out var _);

                // Assert
                Assert.NotNull(aiConfiguration.TelemetryConfiguration.ApplicationIdProvider);
                Assert.IsType(
                    typeof(ApplicationInsightsApplicationIdProvider),
                    aiConfiguration.TelemetryConfiguration.ApplicationIdProvider);
            }

            [Fact]
            public void ConfiguresDefaultSink()
            {
                // Act
                var aiConfiguration = DefaultDependenciesModule.ConfigureApplicationInsights(
                    _appConfiguration,
                    out var _);

                Assert.NotNull(aiConfiguration.TelemetryConfiguration.DefaultTelemetrySink);
                Assert.IsType(
                    typeof(ServerTelemetryChannel),
                    aiConfiguration.TelemetryConfiguration.DefaultTelemetrySink.TelemetryChannel);

                // We can't use Assert.Collection here as Application Insights auto-registers
                // an additional TelemetryProcessor of type TransmissionProcessor in the TelemetrySink,
                // but that type is internal...
                Assert.Contains(
                    aiConfiguration.TelemetryConfiguration.DefaultTelemetrySink.TelemetryProcessors,
                    i => i.GetType().Equals(typeof(QuickPulseTelemetryProcessor)));
                Assert.Contains(
                    aiConfiguration.TelemetryConfiguration.DefaultTelemetrySink.TelemetryProcessors,
                    i => i.GetType().Equals(typeof(AutocollectedMetricsExtractor)));
            }

            private Action<ITelemetryInitializer>[] GetTelemetryInitializerInspectors(string deploymentLabel, string deploymentId)
            {
                var elementInspectors = new List<Action<ITelemetryInitializer>>
                {
                    // Registered by DefaultDependenciesModule in NuGetGallery
                    ti => ti.GetType().Equals(typeof(ClientInformationTelemetryEnricher)),
                    ti => ti.GetType().Equals(typeof(KnownOperationNameEnricher)),
                    ti => ti.GetType().Equals(typeof(AzureWebAppTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(CustomerResourceIdEnricher)),

                    // Registered by applicationinsights.config
                    ti => ti.GetType().Equals(typeof(HttpDependenciesParsingTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(AzureRoleEnvironmentTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(AzureWebAppRoleEnvironmentTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(BuildInfoConfigComponentVersionTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(WebTestTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(SyntheticUserAgentTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(ClientIpHeaderTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(OperationNameTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(Microsoft.ApplicationInsights.Web.OperationCorrelationTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(Microsoft.ApplicationInsights.Extensibility.OperationCorrelationTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(UserTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(AuthenticatedUserIdTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(AccountIdTelemetryInitializer)),
                    ti => ti.GetType().Equals(typeof(SessionTelemetryInitializer)),

                    // Registered by NuGet.Services.Logging
                    ti => ti.GetType().Equals(typeof(TelemetryContextInitializer))
                };

                // Registered by DefaultDependenciesModule in NuGetGallery
                if (deploymentLabel != null)
                {
                    elementInspectors.Add(ti => ti.GetType().Equals(typeof(DeploymentLabelEnricher)));
                }
                if (deploymentId != null)
                {
                    elementInspectors.Add(ti => ti.GetType().Equals(typeof(DeploymentIdTelemetryEnricher)));
                }

                return elementInspectors.ToArray();
            }
        }

        public class TheCreateDbConnectionMethod
        {
            [Fact]
            public void TriesSyncBeforeAsync()
            {
                var connectionFactoryMock = new Mock<ISqlConnectionFactory>();
                connectionFactoryMock
                    .Setup(cf => cf.TryCreate(out It.Ref<SqlConnection>.IsAny))
                    .Returns(true);

                DefaultDependenciesModule.CreateDbConnection(connectionFactoryMock.Object, Mock.Of<ITelemetryService>());
                connectionFactoryMock
                    .Verify(cf => cf.TryCreate(out It.Ref<SqlConnection>.IsAny), Times.Once);
                connectionFactoryMock
                    .Verify(cf => cf.CreateAsync(), Times.Never);
            }

            [Fact]
            public void FallsBackToAsyncIfSyncFails()
            {
                var connectionFactoryMock = new Mock<ISqlConnectionFactory>();
                connectionFactoryMock
                    .Setup(cf => cf.TryCreate(out It.Ref<SqlConnection>.IsAny))
                    .Returns(false);
                connectionFactoryMock
                    .Setup(cf => cf.CreateAsync())
                    .ReturnsAsync((SqlConnection)null);

                DefaultDependenciesModule.CreateDbConnection(connectionFactoryMock.Object, Mock.Of<ITelemetryService>());
                connectionFactoryMock
                    .Verify(cf => cf.TryCreate(out It.Ref<SqlConnection>.IsAny), Times.Once);
                connectionFactoryMock
                    .Verify(cf => cf.CreateAsync(), Times.Once);
            }
        }
    }
}
