// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Parse
{
    public class IncidentRegexParserTests
    {
        public class TheTryParseIncidentMethod
            : IncidentRegexParserTest
        {
            [Fact]
            public void ReturnsFalseIfRegexFailure()
            {
                var incident = new Incident();

                // The Regex matching will throw an ArgumentNullException because the incident's title and the handler's Regex pattern are null.
                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.False(result);
            }

            [Fact]
            public void ReturnsFalseIfMatchUnsuccessful()
            {
                var incident = new Incident
                {
                    Title = "title"
                };

                Handler
                    .Setup(x => x.RegexPattern)
                    .Returns("not title");

                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.False(result);
            }

            [Fact]
            public void ReturnsFalseIfFilterFails()
            {
                var incident = new Incident
                {
                    Title = "title"
                };

                Handler
                    .Setup(x => x.RegexPattern)
                    .Returns(incident.Title);

                var successfulFilter = CreateFilter(incident, true);
                var failureFilter = CreateFilter(incident, false);
                var filters = new[] { successfulFilter, failureFilter };
                Handler
                    .Setup(x => x.Filters)
                    .Returns(filters
                        .Select(f => f.Object)
                        .ToList());

                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.False(result);

                foreach (var filter in filters)
                {
                    filter.Verify();
                }
            }

            [Fact]
            public void ReturnsFalseIfPathCannotBeParsed()
            {
                var incident = new Incident
                {
                    Title = "title"
                };

                Handler
                    .Setup(x => x.RegexPattern)
                    .Returns(incident.Title);

                var filter = CreateFilter(incident, true);
                Handler
                    .Setup(x => x.Filters)
                    .Returns(new[] { filter.Object }.ToList());

                var path = "path";
                Handler
                    .Setup(x => x.TryParseAffectedComponentPath(incident, It.IsAny<GroupCollection>(), out path))
                    .Returns(false)
                    .Verifiable();

                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.False(result);

                filter.Verify();

                Handler.Verify();
            }

            [Fact]
            public void ReturnsFalseIfStatusCannotBeParsed()
            {
                var incident = new Incident
                {
                    Title = "title"
                };

                Handler
                    .Setup(x => x.RegexPattern)
                    .Returns(incident.Title);

                var filter = CreateFilter(incident, true);
                Handler
                    .Setup(x => x.Filters)
                    .Returns(new[] { filter.Object }.ToList());

                var path = "path";
                Handler
                    .Setup(x => x.TryParseAffectedComponentPath(incident, It.IsAny<GroupCollection>(), out path))
                    .Returns(true)
                    .Verifiable();

                var status = (ComponentStatus)99;
                Handler
                    .Setup(x => x.TryParseAffectedComponentStatus(incident, It.IsAny<GroupCollection>(), out status))
                    .Returns(false)
                    .Verifiable();

                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.False(result);

                filter.Verify();

                Handler.Verify();
            }

            [Fact]
            public void ReturnsTrueIfSuccessful()
            {
                var incident = new Incident
                {
                    Id = "id",
                    Title = "title",

                    Source = new IncidentSourceData
                    {
                        CreateDate = new DateTime(2018, 10, 11)
                    },

                    MitigationData = new IncidentStateChangeEventData
                    {
                        Date = new DateTime(2018, 10, 12)
                    }
                };

                Handler
                    .Setup(x => x.RegexPattern)
                    .Returns(incident.Title);

                var filter = CreateFilter(incident, true);
                Handler
                    .Setup(x => x.Filters)
                    .Returns(new[] { filter.Object }.ToList());

                var path = "path";
                Handler
                    .Setup(x => x.TryParseAffectedComponentPath(incident, It.IsAny<GroupCollection>(), out path))
                    .Returns(true)
                    .Verifiable();

                var status = (ComponentStatus)99;
                Handler
                    .Setup(x => x.TryParseAffectedComponentStatus(incident, It.IsAny<GroupCollection>(), out status))
                    .Returns(true)
                    .Verifiable();

                var result = Parser.TryParseIncident(incident, out var parsedIncident);

                Assert.True(result);

                Assert.Equal(incident.Id, parsedIncident.Id);
                Assert.Equal(incident.Source.CreateDate, parsedIncident.StartTime);
                Assert.Equal(incident.MitigationData.Date, parsedIncident.EndTime);
                Assert.Equal(path, parsedIncident.AffectedComponentPath);
                Assert.Equal(status, parsedIncident.AffectedComponentStatus);

                filter.Verify();

                Handler.Verify();
            }

            private Mock<IIncidentRegexParsingFilter> CreateFilter(Incident incident, bool result)
            {
                var filter = new Mock<IIncidentRegexParsingFilter>();
                filter
                    .Setup(x => x.ShouldParse(
                        incident,
                        It.IsAny<GroupCollection>()))
                    .Returns(result)
                    .Verifiable();

                return filter;
            }
        }

        public class IncidentRegexParserTest
        {
            public Mock<IIncidentRegexParsingHandler> Handler { get; }

            public IncidentRegexParser Parser { get; }

            public IncidentRegexParserTest()
            {
                Handler = new Mock<IIncidentRegexParsingHandler>();

                Parser = new IncidentRegexParser(
                    Handler.Object, 
                    Mock.Of<ILogger<IncidentRegexParser>>());
            }
        }
    }
}
