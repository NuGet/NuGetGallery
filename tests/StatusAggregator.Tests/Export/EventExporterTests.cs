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
    public class EventExporterTests
    {
        public class TheExportMethod : EventMessageExporterTest
        {
            [Fact]
            public void IgnoresEventsWithoutMessages()
            {
                var eventEntity = new EventEntity("", DefaultStartTime);

                Table.SetupQuery<MessageEntity>();

                var result = Exporter.Export(EventEntity);

                Assert.Null(result);
            }

            [Fact]
            public void ExportsEventMessagesWithContent()
            {
                var differentEvent = new EventEntity("", DefaultStartTime + TimeSpan.FromDays(1));
                var differentEventMessage = new MessageEntity(differentEvent, DefaultStartTime, "", MessageType.Manual);

                var emptyMessage = new MessageEntity(EventEntity, DefaultStartTime, "", MessageType.Manual);
                var firstMessage = new MessageEntity(EventEntity, DefaultStartTime, "hi", MessageType.Manual);
                var secondMessage = new MessageEntity(EventEntity, DefaultStartTime + TimeSpan.FromDays(1), "hi", MessageType.Manual);

                Table.SetupQuery(differentEventMessage, secondMessage, firstMessage, emptyMessage);

                var result = Exporter.Export(EventEntity);

                Assert.Equal(EventEntity.AffectedComponentPath, result.AffectedComponentPath);
                Assert.Equal(EventEntity.StartTime, result.StartTime);
                Assert.Equal(EventEntity.EndTime, result.EndTime);

                Assert.Equal(2, result.Messages.Count());
                AssertMessageEqual(firstMessage, result.Messages.First());
                AssertMessageEqual(secondMessage, result.Messages.Last());
            }

            private void AssertMessageEqual(MessageEntity expected, Message actual)
            {
                Assert.Equal(expected.Time, actual.Time);
                Assert.Equal(expected.Contents, actual.Contents);
            }
        }

        public class EventMessageExporterTest
        {
            public DateTime DefaultStartTime = new DateTime(2018, 9, 12);
            public EventEntity EventEntity { get; }
            public Mock<ITableWrapper> Table { get; }
            public EventExporter Exporter { get; }

            public EventMessageExporterTest()
            {
                EventEntity = new EventEntity("", DefaultStartTime);

                Table = new Mock<ITableWrapper>();

                Exporter = new EventExporter(
                    Table.Object,
                    Mock.Of<ILogger<EventExporter>>());
            }
        }
    }
}