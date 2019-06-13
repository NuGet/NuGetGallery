// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class ClientInformationTelemetryEnricherTests
    {
        private class TestableClientInformationTelemetryEnricher : ClientInformationTelemetryEnricher
        {
            private readonly HttpContextBase _httpContextBase;

            public TestableClientInformationTelemetryEnricher(HttpContextBase httpContextBase)
            {
                _httpContextBase = httpContextBase;
            }

            protected override HttpContextBase GetHttpContext()
            {
                return _httpContextBase;
            }
        }

        [Theory]
        [InlineData(typeof(RequestTelemetry))]
        [InlineData(typeof(DependencyTelemetry))]
        [InlineData(typeof(TraceTelemetry))]
        [InlineData(typeof(ExceptionTelemetry))]
        public void EnrichesOnlyRequestsTelemetry(Type telemetryType)
        {
            // Arrange
            var telemetry = (ITelemetry)telemetryType.GetConstructor(new Type[] { }).Invoke(new object[] { });
            telemetry.Context.GlobalProperties.Add("Test", "blala");

            var headers = new NameValueCollection
            {
                { GalleryConstants.NuGetProtocolHeaderName, "1.0.0" },
                { GalleryConstants.ClientVersionHeaderName, "1.0.0" },
                { ServicesConstants.UserAgentHeaderName, "NuGet Command Line/4.1.0 (Microsoft Windows NT 6.2.9200.0)" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            if (telemetry is RequestTelemetry)
            {
                Assert.Equal(5, telemetry.Context.GlobalProperties.Count);
            }
            else
            {
                Assert.Equal(1, telemetry.Context.GlobalProperties.Count);
            }
        }

        [Fact]
        public void EnrichesTelemetryWithClientVersion()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { GalleryConstants.ClientVersionHeaderName, "5.0.0" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("5.0.0", telemetry.Context.GlobalProperties[TelemetryService.ClientVersion]);
        }

        [Fact]
        public void EnrichesTelemetryWithProtocolVersion()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { GalleryConstants.NuGetProtocolHeaderName, "5.0.0" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("5.0.0", telemetry.Context.GlobalProperties[TelemetryService.ProtocolVersion]);
        }

        [Fact]
        public void EnrichesTelemetryWithClientInfo()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { ServicesConstants.UserAgentHeaderName, "user agent" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.NotEmpty(telemetry.Context.GlobalProperties[TelemetryService.ClientInformation]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EnrichesTelemetryWithIsAuthenticated(bool isAuthenticated)
        {
            // Arrange
            var telemetry = new RequestTelemetry();
            var enricher = CreateTestEnricher(new NameValueCollection(), isAuthenticated);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal(isAuthenticated, bool.Parse(telemetry.Context.GlobalProperties[TelemetryService.IsAuthenticated]));
        }

        private TestableClientInformationTelemetryEnricher CreateTestEnricher(NameValueCollection headers, bool isAuthenticated = false)
        {
            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.Headers).Returns(headers);
            httpRequest.SetupGet(r => r.IsAuthenticated).Returns(isAuthenticated);

            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

           return new TestableClientInformationTelemetryEnricher(httpContext.Object);
        }
    }

    
}
