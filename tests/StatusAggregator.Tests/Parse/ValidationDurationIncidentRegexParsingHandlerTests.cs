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
    public class ValidationDurationIncidentRegexParsingHandlerTests
    {
        public class TheTryParseAffectedComponentPathMethod
            : ValidationDurationIncidentRegexParsingHandlerTest
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
            : ValidationDurationIncidentRegexParsingHandlerTest
        {
            [Fact]
            public void ReturnsExpected()
            {
                var result = Handler.TryParseAffectedComponentStatus(Incident, Groups, out var status);

                Assert.True(result);

                Assert.Equal(ComponentStatus.Degraded, status);
            }
        }

        public class ValidationDurationIncidentRegexParsingHandlerTest
        {
            public static Incident Incident = new Incident();
            public static GroupCollection Groups = Match.Empty.Groups;

            public ValidationDurationIncidentRegexParsingHandler Handler { get; }

            public ValidationDurationIncidentRegexParsingHandlerTest()
            {
                var environmentFilter = IncidentParsingHandlerTestUtility.CreateEnvironmentFilter();
                Handler = Construct(new[] { environmentFilter });
            }
        }

        public class TheConstructor
            : EnvironmentPrefixIncidentRegexParsingHandlerTests.TheConstructor<ValidationDurationIncidentRegexParsingHandler>
        {
            protected override ValidationDurationIncidentRegexParsingHandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters)
            {
                return ValidationDurationIncidentRegexParsingHandlerTests.Construct(filters.ToArray());
            }
        }

        public static ValidationDurationIncidentRegexParsingHandler Construct(params IIncidentRegexParsingFilter[] filters)
        {
            return new ValidationDurationIncidentRegexParsingHandler(
                filters,
                Mock.Of<ILogger<ValidationDurationIncidentRegexParsingHandler>>());
        }
    }
}
