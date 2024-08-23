// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using Xunit;
using Match = System.Text.RegularExpressions.Match;

namespace StatusAggregator.Tests.Parse
{
    public class TrafficManagerEndpointStatusIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod
            : TrafficManagerEndpointStatusIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsFalseIfUnexpectedValues()
            {
                var title = "[environment] Traffic Manager for domain is reporting target as not Online!";
                IncidentParsingHandlerTestUtility.AssertTryParseAffectedComponentPath(
                    Handler, new Incident { Title = title }, false, null);
            }

            private static readonly string[] GalleryUsncPath = new[]
            {
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.GalleryName,
                NuGetServiceComponentFactory.UsncInstanceName
            };

            private static readonly string[] GalleryUsscPath = new[]
            {
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.GalleryName,
                NuGetServiceComponentFactory.UsscInstanceName
            };

            private static readonly string[] RestoreV3GlobalPath = new[]
            {
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.RestoreName,
                NuGetServiceComponentFactory.V3ProtocolName,
                NuGetServiceComponentFactory.GlobalRegionName
            };

            private static readonly string[] RestoreV3ChinaPath = new[]
            {
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.RestoreName,
                NuGetServiceComponentFactory.V3ProtocolName,
                NuGetServiceComponentFactory.ChinaRegionName
            };

            public static IEnumerable<object[]> ReturnsExpected_Data => TrafficManagerMapping
                .Select(m => new object[] { m.Item1, m.Item2, m.Item3, m.Item4 });
            private static readonly IEnumerable<Tuple<string, string, string, string[]>> TrafficManagerMapping = 
                new Tuple<string, string, string, string[]>[]
                {
                    CreateMapping(Dev, "devnugettest.trafficmanager.net", "nuget-dev-use2-gallery.cloudapp.net", GalleryUsncPath),
                    CreateMapping(Dev, "devnugettest.trafficmanager.net", "nuget-dev-ussc-gallery.cloudapp.net", GalleryUsscPath),
                    CreateMapping(Dev, "nugetapidev.trafficmanager.net", "az635243.vo.msecnd.net", RestoreV3GlobalPath),
                    CreateMapping(Dev, "nugetapidev.trafficmanager.net", "nugetdevcnredirect.trafficmanager.net", RestoreV3ChinaPath),

                    CreateMapping(Int, "nuget-int-test-failover.trafficmanager.net", "nuget-int-0-v2gallery.cloudapp.net", GalleryUsncPath),
                    CreateMapping(Int, "nuget-int-test-failover.trafficmanager.net", "nuget-int-ussc-gallery.cloudapp.net", GalleryUsscPath),

                    CreateMapping(Prod, "nuget-prod-v2gallery.trafficmanager.net", "nuget-prod-0-v2gallery.cloudapp.net", GalleryUsncPath),
                    CreateMapping(Prod, "nuget-prod-v2gallery.trafficmanager.net", "nuget-prod-ussc-gallery.cloudapp.net", GalleryUsscPath),
                    CreateMapping(Prod, "nugetapiprod.trafficmanager.net", "az320820.vo.msecnd.net", RestoreV3GlobalPath),
                    CreateMapping(Prod, "nugetapiprod.trafficmanager.net", "nugetprodcnredirect.trafficmanager.net", RestoreV3ChinaPath),
                };

            [Theory]
            [MemberData(nameof(ReturnsExpected_Data))]
            public void ReturnsExpected(string environment, string domain, string target, string[] names)
            {
                var title = $"[{environment}] Traffic Manager for {domain} is reporting {target} as not Online!";
                IncidentParsingHandlerTestUtility.AssertTryParseAffectedComponentPath(
                    Handler, new Incident { Title = title }, true, ComponentUtility.GetPath(names));
            }

            private static Tuple<string, string, string, string[]> CreateMapping(string environment, string domain, string target, string[] names)
            {
                return Tuple.Create(environment, domain, target, names);
            }
        }

        public class TheTryParsedAffectedComponentStatusMethod
            : TrafficManagerEndpointStatusIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpected()
            {
                var result = Handler.TryParseAffectedComponentStatus(new Incident(), Match.Empty.Groups, out var status);
                Assert.True(result);
                Assert.Equal(ComponentStatus.Down, status);
            }
        }

        public class TrafficManagerEndpointStatusIncidentRegexParsingHandlerTest
        {
            public const string Dev = "dev";
            public const string Int = "int";
            public const string Prod = "prod";

            public TrafficManagerEndpointStatusIncidentRegexParsingHandler Handler { get; }

            public TrafficManagerEndpointStatusIncidentRegexParsingHandlerTest()
            {
                Handler = new TrafficManagerEndpointStatusIncidentRegexParsingHandler(
                    new[] { IncidentParsingHandlerTestUtility.CreateEnvironmentFilter(Dev, Int, Prod) },
                    Mock.Of<ILogger<TrafficManagerEndpointStatusIncidentRegexParsingHandler>>());
            }
        }

        public class TheConstructor
            : EnvironmentPrefixIncidentRegexParsingHandlerTests.TheConstructor<TrafficManagerEndpointStatusIncidentRegexParsingHandler>
        {
            protected override TrafficManagerEndpointStatusIncidentRegexParsingHandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters)
            {
                return TrafficManagerEndpointStatusIncidentRegexParsingHandlerTests.Construct(filters.ToArray());
            }
        }

        public static TrafficManagerEndpointStatusIncidentRegexParsingHandler Construct(params IIncidentRegexParsingFilter[] filters)
        {
            return new TrafficManagerEndpointStatusIncidentRegexParsingHandler(
                filters,
                Mock.Of<ILogger<TrafficManagerEndpointStatusIncidentRegexParsingHandler>>());
        }
    }
}
