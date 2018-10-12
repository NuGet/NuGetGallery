// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Messages;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using Xunit;

namespace StatusAggregator.Tests.Messages
{
    public class IncidentGroupMessageFilterTests
    {
        public class TheCanPostMessagesMethod : IncidentGroupMessageFilterTest
        {
            public static IEnumerable<object[]> ReturnsFalseIfDurationOfGroupTooShort_Data
            {
                get
                {
                    var activeGroup = new IncidentGroupEntity
                    {
                        StartTime = Cursor - StartMessageDelay + TimeSpan.FromTicks(1)
                    };

                    yield return new object[] { activeGroup };

                    var inactiveGroup = new IncidentGroupEntity
                    {
                        StartTime = Cursor - StartMessageDelay + TimeSpan.FromTicks(1),
                        EndTime = Cursor
                    };

                    yield return new object[] { inactiveGroup };
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsFalseIfDurationOfGroupTooShort_Data))]
            public void ReturnsFalseIfDurationOfGroupTooShort(IncidentGroupEntity group)
            {
                var result = Filter.CanPostMessages(group, Cursor);

                Assert.False(result);

                Table
                    .Verify(
                        x => x.CreateQuery<IncidentEntity>(),
                        Times.Never());
            }

            [Fact]
            public void ReturnsFalseIfNoMatchingIncidents()
            {
                var parentRowKey = "parentRowKey";
                var group = new IncidentGroupEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    RowKey = parentRowKey
                };

                var unlinkedIncident = new IncidentEntity
                {
                    StartTime = Cursor,
                    ParentRowKey = "something else"
                };

                var shortIncident = new IncidentEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    EndTime = Cursor - TimeSpan.FromTicks(1),
                    ParentRowKey = parentRowKey
                };
                
                Table.SetupQuery(unlinkedIncident, shortIncident);

                var result = Filter.CanPostMessages(group, Cursor);

                Assert.False(result);
            }

            [Fact]
            public void ReturnsTrueIfActiveIncident()
            {
                var parentRowKey = "parentRowKey";
                var group = new IncidentGroupEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    RowKey = parentRowKey
                };

                var unlinkedIncident = new IncidentEntity
                {
                    StartTime = Cursor,
                    ParentRowKey = "something else"
                };

                var shortIncident = new IncidentEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    EndTime = Cursor - TimeSpan.FromTicks(1),
                    ParentRowKey = parentRowKey
                };

                var activeIncident = new IncidentEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    ParentRowKey = parentRowKey
                };
                
                Table.SetupQuery(unlinkedIncident, shortIncident, activeIncident);

                var result = Filter.CanPostMessages(group, Cursor);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsTrueIfLongIncident()
            {
                var parentRowKey = "parentRowKey";
                var group = new IncidentGroupEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    RowKey = parentRowKey
                };

                var unlinkedIncident = new IncidentEntity
                {
                    StartTime = Cursor,
                    ParentRowKey = "something else"
                };

                var shortIncident = new IncidentEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    EndTime = Cursor - TimeSpan.FromTicks(1),
                    ParentRowKey = parentRowKey
                };

                var longIncident = new IncidentEntity
                {
                    StartTime = Cursor - StartMessageDelay,
                    EndTime = Cursor,
                    ParentRowKey = parentRowKey
                };

                Table.SetupQuery(unlinkedIncident, shortIncident, longIncident);

                var result = Filter.CanPostMessages(group, Cursor);

                Assert.True(result);
            }
        }

        public class IncidentGroupMessageFilterTest
        {
            public static readonly DateTime Cursor = new DateTime(2018, 9, 13);
            public static readonly TimeSpan StartMessageDelay = TimeSpan.FromDays(1);

            public Mock<ITableWrapper> Table { get; }
            public IncidentGroupMessageFilter Filter { get; }

            public IncidentGroupMessageFilterTest()
            {
                Table = new Mock<ITableWrapper>();

                var config = new StatusAggregatorConfiguration
                {
                    EventStartMessageDelayMinutes = (int)StartMessageDelay.TotalMinutes
                };

                Filter = new IncidentGroupMessageFilter(
                    Table.Object,
                    config,
                    Mock.Of<ILogger<IncidentGroupMessageFilter>>());
            }
        }
    }
}