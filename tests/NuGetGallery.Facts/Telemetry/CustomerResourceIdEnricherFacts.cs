// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Telemetry
{
    public class CustomerResourceIdEnricherFacts
    {
        private class TestableCustomerResourceIdEnricher : CustomerResourceIdEnricher
        {
            private readonly HttpContextBase _httpContextBase;

            public TestableCustomerResourceIdEnricher(HttpContextBase httpContextBase)
            {
                _httpContextBase = httpContextBase;
            }

            protected override HttpContextBase GetHttpContext()
            {
                return _httpContextBase;
            }
        }

        [Theory]
        [InlineData(typeof(RequestTelemetry), 0)]
        [InlineData(typeof(DependencyTelemetry), 0)]
        [InlineData(typeof(TraceTelemetry), 0)]
        [InlineData(typeof(ExceptionTelemetry), 0)]
        [InlineData(typeof(MetricTelemetry), 1)]
        public void EnrichesOnlyMetricTelemetry(Type telemetryType, int addedProperties)
        {
            // Arrange
            var telemetry = (ITelemetry)Activator.CreateInstance(telemetryType);
            if (telemetry is MetricTelemetry metric)
            {
                metric.Name = "PackagePush";
            }

            var itemTelemetry = telemetry as ISupportProperties;
            itemTelemetry.Properties.Add("Test", "blala");

            var enricher = CreateTestEnricher(new Dictionary<string, string> { { MicrosoftClaims.TenantId, "tenant-id" } });

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal(1 + addedProperties, itemTelemetry.Properties.Count);
        }

        [Fact]
        public void DoesNotEnrichMetricNotInAllowList()
        {
            // Arrange
            var telemetry = new MetricTelemetry { Name = "SomethingElse" };

            var enricher = CreateTestEnricher(new Dictionary<string, string> { { MicrosoftClaims.TenantId, "tenant-id" } });

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Empty(telemetry.Properties);
        }

        [Theory]
        [MemberData(nameof(MetricNames))]
        public void EnrichesTelemetryWithTenantId(string name)
        {
            // Arrange
            var telemetry = new MetricTelemetry { Name = name };

            var enricher = CreateTestEnricher(new Dictionary<string, string> { { MicrosoftClaims.TenantId, "tenant-id" } });

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("/tenants/tenant-id", telemetry.Properties["CustomerResourceId"]);
        }

        [Theory]
        [MemberData(nameof(MetricNames))]
        public void EnrichesTelemetryWithEmptyWhenTenantIdIsNotInClaims(string name)
        {
            // Arrange
            var telemetry = new MetricTelemetry { Name = name };

            var enricher = CreateTestEnricher(new Dictionary<string, string>());

            // Act
            enricher.Initialize(telemetry);

            // Assert
            Assert.Equal("/tenants/00000000-0000-0000-0000-000000000000", telemetry.Properties["CustomerResourceId"]);
        }

        private TestableCustomerResourceIdEnricher CreateTestEnricher(IReadOnlyDictionary<string, string> claims)
        {
            var claimsIdentity = new ClaimsIdentity(claims.Select(x => new Claim(x.Key, x.Value)));

            var principal = new Mock<IPrincipal>();
            principal.Setup(x => x.Identity).Returns(claimsIdentity);

            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.User).Returns(principal.Object);

            return new TestableCustomerResourceIdEnricher(httpContext.Object);
        }

        public static IEnumerable<object[]> MetricNames => new[]
        {
            new object[] { "PackagePush" },
            new object[] { "PackagePushFailure" },
            new object[] { "PackagePushDisconnect" },
            new object[] { "SymbolPackagePush" },
            new object[] { "SymbolPackagePushFailure" },
            new object[] { "SymbolPackagePushDisconnect" },
        };
    }
}
