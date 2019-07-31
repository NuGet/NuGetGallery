// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Parse;
using Xunit;
using Match = System.Text.RegularExpressions.Match;

namespace StatusAggregator.Tests.Parse
{
    public class AIAvailabilityIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod : AIAvailabilityIncidentRegexParsingHandlerTest
        {
            [Theory]
            [InlineData("blah blah blah blah", false, "")]
            [InlineData("[env] AI Availability test 'test' is failing!", false, "")]
            [InlineData("[env] AI Availability test '' is failing! (path)", false, "")]
            [InlineData("[env] AI Availability test 'test' is failing! ()", false, "")]
            [InlineData("[env] AI Availability test 'test' is failing! (path)", true, "path")]
            public void ReturnsExpectedResponse(string title, bool success, string affectedComponentPath)
            {
                var incident = new Incident { Title = title };
                IncidentParsingHandlerTestUtility.AssertTryParseAffectedComponentPath(
                    Handler, incident, success, affectedComponentPath);
            }
        }

        public class TheTryParseAffectedComponentStatusMethod : AIAvailabilityIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpectedResponse()
            {
                var result = Handler.TryParseAffectedComponentStatus(new Incident(), Match.Empty.Groups, out var status);
                Assert.True(result);
                Assert.Equal(ComponentStatus.Down, status);
            }
        }

        public class AIAvailabilityIncidentRegexParsingHandlerTest
        {
            public string Environment = "env";
            public AIAvailabilityIncidentRegexParsingHandler Handler { get; }

            public AIAvailabilityIncidentRegexParsingHandlerTest()
            {
                Handler = Construct(
                    new[] { IncidentParsingHandlerTestUtility.CreateEnvironmentFilter(Environment) });
            }
        }

        public class TheConstructor
            : EnvironmentPrefixIncidentRegexParsingHandlerTests.TheConstructor<AIAvailabilityIncidentRegexParsingHandler>
        {
            protected override AIAvailabilityIncidentRegexParsingHandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters)
            {
                return AIAvailabilityIncidentRegexParsingHandlerTests.Construct(filters.ToArray());
            }
        }

        public static AIAvailabilityIncidentRegexParsingHandler Construct(params IIncidentRegexParsingFilter[] filters)
        {
            return new AIAvailabilityIncidentRegexParsingHandler(
                filters,
                Mock.Of<ILogger<AIAvailabilityIncidentRegexParsingHandler>>());
        }
    }
}
