// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    public class PingdomIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod
            : PingdomIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsFalseIfUnexpectedCheck()
            {
                var match = ParsingUtility.GetMatchWithGroup(
                    PingdomIncidentRegexParsingHandler.CheckNameGroupName,
                    "invalid");

                var result = Handler.TryParseAffectedComponentPath(Incident, match.Groups, out var path);

                Assert.False(result);
            }

            public static IEnumerable<object[]> ReturnsExpectedPath_Data => CheckNameMapping.Select(p => new object[] { p.Key, p.Value });
            private static IDictionary<string, string[]> CheckNameMapping = new Dictionary<string, string[]>
            {
                {
                    "CDN DNS",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName, 
                        NuGetServiceComponentFactory.RestoreName, 
                        NuGetServiceComponentFactory.V3ProtocolName)
                },

                {
                    "CDN Global",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V3ProtocolName,
                        NuGetServiceComponentFactory.GlobalRegionName)
                },

                {
                    "CDN China",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V3ProtocolName,
                        NuGetServiceComponentFactory.ChinaRegionName)
                },

                {
                    "Gallery DNS",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName)
                },

                {
                    "Gallery Home",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName)
                },

                {
                    "Gallery USNC /",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName,
                        NuGetServiceComponentFactory.UsncInstanceName)
                },

                {
                    "Gallery USNC /Packages",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName,
                        NuGetServiceComponentFactory.UsncInstanceName)
                },

                {
                    "Gallery USSC /",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName,
                        NuGetServiceComponentFactory.UsscInstanceName)
                },

                {
                    "Gallery USSC /Packages",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.GalleryName,
                        NuGetServiceComponentFactory.UsscInstanceName)
                },

                {
                    "Gallery USNC /api/v2/Packages()",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V2ProtocolName,
                        NuGetServiceComponentFactory.UsncInstanceName)
                },

                {
                    "Gallery USNC /api/v2/package/NuGet.GalleryUptime/1.0.0",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V2ProtocolName,
                        NuGetServiceComponentFactory.UsncInstanceName)
                },

                {
                    "Gallery USSC /api/v2/Packages()",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V2ProtocolName,
                        NuGetServiceComponentFactory.UsscInstanceName)
                },

                {
                    "Gallery USSC /api/v2/package/NuGet.GalleryUptime/1.0.0",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.RestoreName,
                        NuGetServiceComponentFactory.V2ProtocolName,
                        NuGetServiceComponentFactory.UsscInstanceName)
                },

                {
                    "Search USNC /query",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.SearchName,
                        NuGetServiceComponentFactory.GlobalRegionName,
                        NuGetServiceComponentFactory.UsncInstanceName)
                },

                {
                    "Search USSC /query",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.SearchName,
                        NuGetServiceComponentFactory.GlobalRegionName,
                        NuGetServiceComponentFactory.UsscInstanceName)
                },

                {
                    "Search EA /query",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.SearchName,
                        NuGetServiceComponentFactory.ChinaRegionName,
                        NuGetServiceComponentFactory.EaInstanceName)
                },

                {
                    "Search SEA /query",
                    CombineNames(
                        NuGetServiceComponentFactory.RootName,
                        NuGetServiceComponentFactory.SearchName,
                        NuGetServiceComponentFactory.ChinaRegionName,
                        NuGetServiceComponentFactory.SeaInstanceName)
                },
            };

            [Theory]
            [MemberData(nameof(ReturnsExpectedPath_Data))]
            public void ReturnsExpectedPath(string checkName, string[] names)
            {
                var match = ParsingUtility.GetMatchWithGroup(
                    PingdomIncidentRegexParsingHandler.CheckNameGroupName, 
                    checkName);

                var result = Handler.TryParseAffectedComponentPath(Incident, match.Groups, out var path);

                Assert.True(result);
                Assert.Equal(ComponentUtility.GetPath(names), path);
            }

            private static string[] CombineNames(params string[] names)
            {
                return names;
            }
        }

        public class TheTryParseAffectedComponentStatusMethod
            : PingdomIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpected()
            {
                var result = Handler.TryParseAffectedComponentStatus(Incident, Match.Empty.Groups, out var status);

                Assert.True(result);
                Assert.Equal(ComponentStatus.Degraded, status);
            }
        }

        public class PingdomIncidentRegexParsingHandlerTest
        {
            public Incident Incident = new Incident();
            public PingdomIncidentRegexParsingHandler Handler { get; }

            public PingdomIncidentRegexParsingHandlerTest()
            {
                Handler = new PingdomIncidentRegexParsingHandler(
                    Enumerable.Empty<IIncidentRegexParsingFilter>(),
                    Mock.Of<ILogger<PingdomIncidentRegexParsingHandler>>());
            }
        }
    }
}
