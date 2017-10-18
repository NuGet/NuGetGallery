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
            telemetry.Context.Properties.Add("Test", "blala");

            var headers = new NameValueCollection
            {
                { Constants.NuGetProtocolHeaderName, "1.0.0" },
                { Constants.ClientVersionHeaderName, "1.0.0" },
                { Constants.UserAgentHeaderName, "NuGet Command Line/4.1.0 (Microsoft Windows NT 6.2.9200.0)" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            if (telemetry is RequestTelemetry)
            {
                Assert.Equal(4, telemetry.Context.Properties.Count);
            }
            else
            {
                Assert.Equal(1, telemetry.Context.Properties.Count);
            }
        }

        [Fact]
        public void EnrichesTelemetryWithClientVersion()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { Constants.ClientVersionHeaderName, "5.0.0" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("5.0.0", telemetry.Properties[TelemetryService.ClientVersion]);
        }

        [Fact]
        public void EnrichesTelemetryWithProtocolVersion()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { Constants.NuGetProtocolHeaderName, "5.0.0" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("5.0.0", telemetry.Properties[TelemetryService.ProtocolVersion]);
        }

        [Fact]
        public void EnrichesTelemetryWithClientInfo()
        {
            // Arrange
            var telemetry = new RequestTelemetry();

            var headers = new NameValueCollection
            {
                { Constants.UserAgentHeaderName, "user agent" }
            };

            var enricher = CreateTestEnricher(headers);

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.NotEmpty(telemetry.Properties[TelemetryService.ClientInformation]);
        }

        private TestableClientInformationTelemetryEnricher CreateTestEnricher(NameValueCollection headers)
        {
            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.Headers).Returns(headers);

            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

           return new TestableClientInformationTelemetryEnricher(httpContext.Object);
        }
    }

    
}
