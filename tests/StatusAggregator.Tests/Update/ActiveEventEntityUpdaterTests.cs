// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class ActiveEventEntityUpdaterTests
    {
        public class TheUpdateAllAsyncMethod
            : ActiveEventEntityUpdaterTest
        {
            [Fact]
            public async Task UpdatesAllActiveEvents()
            {
                var cursor = new DateTime(2018, 10, 10);

                var inactiveEvent = new EventEntity
                {
                    RowKey = "inactive",
                    EndTime = new DateTime(2018, 10, 11)
                };

                var activeEvent1 = new EventEntity
                {
                    RowKey = "active1"
                };

                var activeEvent2 = new EventEntity
                {
                    RowKey = "active2"
                };

                Table.SetupQuery(inactiveEvent, activeEvent1, activeEvent2);

                EventUpdater
                    .Setup(x => x.UpdateAsync(activeEvent1, cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                EventUpdater
                    .Setup(x => x.UpdateAsync(activeEvent2, cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAllAsync(cursor);

                EventUpdater.Verify();

                EventUpdater
                    .Verify(
                        x => x.UpdateAsync(inactiveEvent, It.IsAny<DateTime>()),
                        Times.Never());
            }
        }

        public class ActiveEventEntityUpdaterTest
        {
            public Mock<ITableWrapper> Table { get; }
            public Mock<IComponentAffectingEntityUpdater<EventEntity>> EventUpdater { get; }

            public ActiveEventEntityUpdater Updater { get; }

            public ActiveEventEntityUpdaterTest()
            {
                Table = new Mock<ITableWrapper>();

                EventUpdater = new Mock<IComponentAffectingEntityUpdater<EventEntity>>();

                Updater = new ActiveEventEntityUpdater(
                    Table.Object,
                    EventUpdater.Object,
                    Mock.Of<ILogger<ActiveEventEntityUpdater>>());
            }
        }
    }
}
