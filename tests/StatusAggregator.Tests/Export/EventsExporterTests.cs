// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Export;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using Xunit;

namespace StatusAggregator.Tests.Export
{
    public class EventsExporterTests
    {
        public class TheExportMethod : EventsExporterTest
        {
            [Fact]
            public void ExportsRecentEvents()
            {
                var oldEventEntity = new EventEntity("", 
                    new DateTime(2017, 9, 12), 
                    endTime: new DateTime(2017, 9, 13));

                var activeEvent1Entity = new EventEntity("", 
                    new DateTime(2017, 9, 12));
                var activeEvent2Entity = new EventEntity("", 
                    Cursor);

                var recentEvent1Entity = new EventEntity("", 
                    Cursor - EventVisibilityPeriod, 
                    endTime: Cursor - EventVisibilityPeriod);
                var recentEvent2Entity = new EventEntity("", 
                    Cursor - EventVisibilityPeriod, 
                    endTime: Cursor);
                
                Table.SetupQuery(
                    oldEventEntity,
                    activeEvent1Entity,
                    activeEvent2Entity,
                    recentEvent1Entity,
                    recentEvent2Entity);

                var eventForActiveEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, new[] { new Message(DateTime.MinValue, "") });
                IndividualExporter
                    .Setup(x => x.Export(activeEvent1Entity))
                    .Returns(eventForActiveEvent1)
                    .Verifiable();

                IndividualExporter
                    .Setup(x => x.Export(activeEvent2Entity))
                    .Returns<Event>(null)
                    .Verifiable();

                var eventForRecentEvent1 = new Event("", DateTime.MinValue, DateTime.MinValue, new[] { new Message(DateTime.MinValue, "") });
                IndividualExporter
                    .Setup(x => x.Export(recentEvent1Entity))
                    .Returns(eventForRecentEvent1)
                    .Verifiable();

                IndividualExporter
                    .Setup(x => x.Export(recentEvent2Entity))
                    .Returns<Event>(null)
                    .Verifiable();

                var result = Exporter.Export(Cursor);

                var expectedEvents = new[] { eventForActiveEvent1, eventForRecentEvent1 };
                Assert.Equal(expectedEvents.Count(), result.Count());
                foreach (var expectedEvent in expectedEvents)
                {
                    Assert.Contains(expectedEvent, result);
                }

                IndividualExporter.Verify();
                IndividualExporter
                    .Verify(
                        x => x.Export(oldEventEntity),
                        Times.Never());
            }
        }

        public class EventsExporterTest
        {
            public DateTime Cursor => new DateTime(2018, 9, 12);
            public TimeSpan EventVisibilityPeriod => TimeSpan.FromDays(10);
            public Mock<ITableWrapper> Table { get; }
            public Mock<IEventExporter> IndividualExporter { get; }
            public EventsExporter Exporter { get; }

            public EventsExporterTest()
            {
                Table = new Mock<ITableWrapper>();

                IndividualExporter = new Mock<IEventExporter>();

                var config = new StatusAggregatorConfiguration()
                {
                    EventVisibilityPeriodDays = EventVisibilityPeriod.Days
                };

                Exporter = new EventsExporter(
                    Table.Object,
                    IndividualExporter.Object,
                    config,
                    Mock.Of<ILogger<EventsExporter>>());
            }
        }
    }
}