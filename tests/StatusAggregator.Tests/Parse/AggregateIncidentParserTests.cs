// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Parse
{
    public class AggregateIncidentParserTests
    {
        public class TheParseIncidentMethod
        {
            private static readonly Incident Incident = new Incident
            {
                Id = "1111111",
                Source = new IncidentSourceData
                {
                    CreateDate = new DateTime(2018, 10, 11)
                }
            };

            [Fact]
            public void ReturnsListOfParsedIncidents()
            {
                var failingParser1 = CreateParser(false);
                var failingParser2 = CreateParser(false);

                var parsedIncident1 = new ParsedIncident(Incident, "one", ComponentStatus.Degraded);
                var successfulParser1 = CreateParser(true, parsedIncident1);

                var parsedIncident2 = new ParsedIncident(Incident, "two", ComponentStatus.Down);
                var successfulParser2 = CreateParser(true, parsedIncident2);

                var parsers = new Mock<IIncidentParser>[]
                {
                    failingParser1,
                    successfulParser1,
                    successfulParser2,
                    failingParser2
                };

                var aggregateParser = new AggregateIncidentParser(
                    parsers.Select(p => p.Object),
                    Mock.Of<ILogger<AggregateIncidentParser>>());

                var result = aggregateParser.ParseIncident(Incident);
                
                foreach (var parser in parsers)
                {
                    parser.Verify();
                }

                Assert.Equal(2, result.Count());
                Assert.Contains(parsedIncident1, result);
                Assert.Contains(parsedIncident2, result);
            }

            private Mock<IIncidentParser> CreateParser(bool result, ParsedIncident returnedIncident = null)
            {
                var parser = new Mock<IIncidentParser>();
                parser
                    .Setup(x => x.TryParseIncident(Incident, out returnedIncident))
                    .Returns(result)
                    .Verifiable();

                return parser;
            }
        }
    }
}
