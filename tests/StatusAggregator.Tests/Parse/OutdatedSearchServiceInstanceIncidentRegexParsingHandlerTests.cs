// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public class OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod
            : OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpected()
            {
                var result = Handler.TryParseAffectedComponentPath(Incident, Groups, out var path);

                Assert.True(result);

                Assert.Equal(
                    ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName),
                    path);
            }
        }

        public class TheTryParseAffectedComponentStatusMethod
            : OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpected()
            {
                var result = Handler.TryParseAffectedComponentStatus(Incident, Groups, out var status);

                Assert.True(result);

                Assert.Equal(ComponentStatus.Degraded, status);
            }
        }

        public class OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTest
        {
            public static Incident Incident = new Incident();
            public static GroupCollection Groups = Match.Empty.Groups;

            public OutdatedSearchServiceInstanceIncidentRegexParsingHandler Handler { get; }

            public OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTest()
            {
                var environmentFilter = IncidentParsingHandlerTestUtility.CreateEnvironmentFilter();
                Handler = Construct(new[] { environmentFilter });
            }
        }

        public class TheConstructor
            : EnvironmentPrefixIncidentRegexParsingHandlerTests.TheConstructor<OutdatedSearchServiceInstanceIncidentRegexParsingHandler>
        {
            [Fact]
            public void IgnoresSeverityFilter()
            {
                var severityFilter = IncidentParsingHandlerTestUtility.CreateSeverityFilter(0);
                var environmentFilter = IncidentParsingHandlerTestUtility.CreateEnvironmentFilter();

                var handler = Construct(new IIncidentRegexParsingFilter[] { severityFilter, environmentFilter });

                Assert.Single(handler.Filters);
                Assert.Contains(environmentFilter, handler.Filters);
                Assert.DoesNotContain(severityFilter, handler.Filters);
            }

            protected override OutdatedSearchServiceInstanceIncidentRegexParsingHandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters)
            {
                return OutdatedSearchServiceInstanceIncidentRegexParsingHandlerTests.Construct(filters.ToArray());
            }
        }

        public static OutdatedSearchServiceInstanceIncidentRegexParsingHandler Construct(params IIncidentRegexParsingFilter[] filters)
        {
            return new OutdatedSearchServiceInstanceIncidentRegexParsingHandler(
                filters,
                Mock.Of<ILogger<OutdatedSearchServiceInstanceIncidentRegexParsingHandler>>());
        }
    }
}
