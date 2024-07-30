// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class MessageChangeEventProviderTests
    {
        public class TheGetMethod : MessageChangeEventProviderTest
        {
            [Fact]
            public void GetsChanges()
            {
                var cursor = new DateTime(2018, 10, 9);
                var eventEntity = new EventEntity
                {
                    RowKey = "rowKey"
                };

                var groupFromDifferentEvent = new IncidentGroupEntity
                {
                    ParentRowKey = "different"
                };

                var filteredGroup = new IncidentGroupEntity
                {
                    ParentRowKey = eventEntity.RowKey
                };

                Filter
                    .Setup(x => x.CanPostMessages(filteredGroup, cursor))
                    .Returns(false);

                var activeGroup = new IncidentGroupEntity
                {
                    ParentRowKey = eventEntity.RowKey,
                    AffectedComponentPath = "path",
                    AffectedComponentStatus = 99,
                    StartTime = new DateTime(2018, 10, 10)
                };

                Filter
                    .Setup(x => x.CanPostMessages(activeGroup, cursor))
                    .Returns(true);

                var inactiveGroup = new IncidentGroupEntity
                {
                    ParentRowKey = eventEntity.RowKey,
                    AffectedComponentPath = "path 2",
                    AffectedComponentStatus = 101,
                    StartTime = new DateTime(2018, 10, 11),
                    EndTime = new DateTime(2018, 10, 12),
                };

                Filter
                    .Setup(x => x.CanPostMessages(inactiveGroup, cursor))
                    .Returns(true);

                Table.SetupQuery(groupFromDifferentEvent, filteredGroup, activeGroup, inactiveGroup);

                var result = Provider.Get(eventEntity, cursor);

                Assert.Equal(3, result.Count());

                var firstChange = result.First();
                AssertChange(activeGroup, MessageType.Start, firstChange);

                var secondChange = result.ElementAt(1);
                AssertChange(inactiveGroup, MessageType.Start, secondChange);

                var thirdChange = result.ElementAt(2);
                AssertChange(inactiveGroup, MessageType.End, thirdChange);
            }

            private void AssertChange(IncidentGroupEntity group, MessageType type, MessageChangeEvent change)
            {
                DateTime expectedTimestamp;
                switch (type)
                {
                    case MessageType.Start:
                        expectedTimestamp = group.StartTime;
                        break;
                    case MessageType.End:
                        expectedTimestamp = group.EndTime.Value;
                        break;
                    default:
                        throw new ArgumentException(nameof(type));
                }

                Assert.Equal(expectedTimestamp, change.Timestamp);
                Assert.Equal(group.AffectedComponentPath, change.AffectedComponentPath);
                Assert.Equal(group.AffectedComponentStatus, (int)change.AffectedComponentStatus);
                Assert.Equal(type, change.Type);
            }
        }

        public class MessageChangeEventProviderTest
        {
            public Mock<ITableWrapper> Table { get; }
            public Mock<IIncidentGroupMessageFilter> Filter { get; }
            
            public MessageChangeEventProvider Provider { get; }

            public MessageChangeEventProviderTest()
            {
                Table = new Mock<ITableWrapper>();

                Filter = new Mock<IIncidentGroupMessageFilter>();

                Provider = new MessageChangeEventProvider(
                    Table.Object,
                    Filter.Object,
                    Mock.Of<ILogger<MessageChangeEventProvider>>());
            }
        }
    }
}
