// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Collector;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Collector
{
    public class IncidentEntityCollectorProcessorTests
    {
        public class TheNameProperty : IncidentEntityCollectorProcessorTest
        {
            [Fact]
            public void ReturnsIncidentsCollectorName()
            {
                Assert.Equal(IncidentEntityCollectorProcessor.IncidentsCollectorName, Processor.Name);
            }
        }

        public abstract class TheFetchSinceMethod : IncidentEntityCollectorProcessorTest
        {
            public abstract DateTime Cursor { get; }

            [Fact]
            public async Task ReturnsNullIfNoIncidents()
            {
                SetupClientQuery(Cursor, Enumerable.Empty<Incident>());

                var result = await Processor.FetchSince(Cursor);

                Assert.Null(result);

                Parser
                    .Verify(
                        x => x.ParseIncident(It.IsAny<Incident>()),
                        Times.Never());

                Factory
                    .Verify(
                        x => x.CreateAsync(It.IsAny<ParsedIncident>()),
                        Times.Never());
            }

            [Fact]
            public async Task DoesNotCreateIncidentsThatCannotBeParsed()
            {
                var unparsableIncident = new Incident()
                {
                    CreateDate = Cursor + TimeSpan.FromMinutes(1)
                };

                SetupClientQuery(
                    Cursor,
                    new[] { unparsableIncident });

                Parser
                    .Setup(x => x.ParseIncident(It.IsAny<Incident>()))
                    .Returns(Enumerable.Empty<ParsedIncident>());

                var result = await Processor.FetchSince(Cursor);

                Assert.Equal(unparsableIncident.CreateDate, result);

                Factory
                    .Verify(
                        x => x.CreateAsync(It.IsAny<ParsedIncident>()),
                        Times.Never());
            }

            [Fact]
            public async Task CreatesParsedIncidents()
            {
                var firstIncident = new Incident()
                {
                    CreateDate = Cursor + TimeSpan.FromMinutes(1),
                    Source = new IncidentSourceData { CreateDate = Cursor + TimeSpan.FromMinutes(5) }
                };

                var secondIncident = new Incident()
                {
                    CreateDate = Cursor + TimeSpan.FromMinutes(2),
                    Source = new IncidentSourceData { CreateDate = Cursor + TimeSpan.FromMinutes(6) }
                };

                var thirdIncident = new Incident()
                {
                    CreateDate = Cursor + TimeSpan.FromMinutes(3),
                    Source = new IncidentSourceData { CreateDate = Cursor + TimeSpan.FromMinutes(4) }
                };

                var incidents = new[] { firstIncident, secondIncident, thirdIncident };

                SetupClientQuery(
                    Cursor,
                    incidents);

                var firstFirstParsedIncident = new ParsedIncident(firstIncident, "", ComponentStatus.Up);
                var firstSecondParsedIncident = new ParsedIncident(firstIncident, "", ComponentStatus.Up);
                var secondFirstParsedIncident = new ParsedIncident(secondIncident, "", ComponentStatus.Up);
                var secondSecondParsedIncident = new ParsedIncident(secondIncident, "", ComponentStatus.Up);
                var thirdParsedIncident = new ParsedIncident(thirdIncident, "", ComponentStatus.Up);

                Parser
                    .Setup(x => x.ParseIncident(firstIncident))
                    .Returns(new[] { firstFirstParsedIncident, firstSecondParsedIncident });

                Parser
                    .Setup(x => x.ParseIncident(secondIncident))
                    .Returns(new[] { secondFirstParsedIncident, secondSecondParsedIncident });

                Parser
                    .Setup(x => x.ParseIncident(thirdIncident))
                    .Returns(new[] { thirdParsedIncident });

                var lastCreateDate = DateTime.MinValue;
                Factory
                    .Setup(x => x.CreateAsync(It.IsAny<ParsedIncident>()))
                    .ReturnsAsync(new IncidentEntity())
                    .Callback<ParsedIncident>(incident =>
                    {
                        var nextCreateDate = incident.StartTime;
                        Assert.True(nextCreateDate >= lastCreateDate);
                        lastCreateDate = nextCreateDate;
                    });

                var result = await Processor.FetchSince(Cursor);

                Assert.Equal(incidents.Max(i => i.CreateDate), result);

                Factory
                    .Verify(
                        x => x.CreateAsync(firstFirstParsedIncident),
                        Times.Once());

                Factory
                    .Verify(
                        x => x.CreateAsync(secondFirstParsedIncident),
                        Times.Once());
            }
        }

        public class TheFetchSinceMethodAtMinValue : TheFetchSinceMethod
        {
            public override DateTime Cursor => DateTime.MinValue;
        }

        public class TheFetchSinceMethodAtPresent : TheFetchSinceMethod
        {
            public override DateTime Cursor => new DateTime(2018, 9, 11);

            [Fact]
            public async Task FiltersOutIncidentsBeforeOrAtCursor()
            {
                SetupClientQuery(
                    Cursor,
                    new[]
                    {
                        new Incident()
                        {
                            CreateDate = Cursor - TimeSpan.FromMinutes(1)
                        },

                        new Incident()
                        {
                            CreateDate = Cursor
                        }
                    });

                var result = await Processor.FetchSince(Cursor);

                Assert.Null(result);

                Parser
                    .Verify(
                        x => x.ParseIncident(It.IsAny<Incident>()),
                        Times.Never());

                Factory
                    .Verify(
                        x => x.CreateAsync(It.IsAny<ParsedIncident>()),
                        Times.Never());
            }
        }

        public class IncidentEntityCollectorProcessorTest
        {
            public string TeamId => "teamId";
            public Mock<IAggregateIncidentParser> Parser { get; }
            public Mock<IIncidentApiClient> Client { get; }
            public Mock<IComponentAffectingEntityFactory<IncidentEntity>> Factory { get; }
            public IncidentEntityCollectorProcessor Processor { get; }

            public IncidentEntityCollectorProcessorTest()
            {
                Parser = new Mock<IAggregateIncidentParser>();

                Client = new Mock<IIncidentApiClient>();

                Factory = new Mock<IComponentAffectingEntityFactory<IncidentEntity>>();

                var config = new StatusAggregatorConfiguration()
                {
                    TeamId = TeamId
                };

                Processor = new IncidentEntityCollectorProcessor(
                    Client.Object,
                    Parser.Object,
                    Factory.Object,
                    config,
                    Mock.Of<ILogger<IncidentEntityCollectorProcessor>>());
            }

            public void SetupClientQuery(DateTime cursor, IEnumerable<Incident> incidents)
            {
                Client
                    .Setup(x => x.GetIncidents(GetExpectedQuery(cursor)))
                    .ReturnsAsync(incidents);
            }

            private string GetExpectedQuery(DateTime cursor)
            {
                var query = $"$filter=OwningTeamId eq '{TeamId}'";

                if (cursor != DateTime.MinValue)
                {
                    query += $" and CreateDate gt datetime'{cursor.ToString("o")}'";
                }

                return query;
            }
        }
    }
}