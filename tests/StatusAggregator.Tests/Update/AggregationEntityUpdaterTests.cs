// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;
using StatusAggregator.Tests.TestUtility;
using StatusAggregator.Update;
using Xunit;

namespace StatusAggregator.Tests.Update
{
    public class AggregationEntityUpdaterTests
    {
        public class TheIncidentGroupUpdateAsyncMethod 
            : TheUpdateAsyncMethod<IncidentEntity, IncidentGroupEntity>
        {
        }

        public class TheEventUpdateAsyncMethod
            : TheUpdateAsyncMethod<IncidentGroupEntity, EventEntity>
        {
        }

        public abstract class TheUpdateAsyncMethod<TChildEntity, TAggregationEntity>
            : AggregationEntityUpdaterTest<TChildEntity, TAggregationEntity>
            where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>, new()
            where TAggregationEntity : ComponentAffectingEntity, new()
        {
            [Fact]
            public async Task ThrowsIfAggregationNull()
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => Updater.UpdateAsync(null, Cursor));
            }

            [Fact]
            public async Task IgnoresDeactivatedAggregation()
            {
                var aggregation = new TAggregationEntity
                {
                    EndTime = new DateTime(2018, 10, 9)
                };

                await Updater.UpdateAsync(aggregation, Cursor);

                Table
                    .Verify(
                        x => x.CreateQuery<TChildEntity>(),
                        Times.Never());

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());

                ChildUpdater
                    .Verify(
                        x => x.UpdateAsync(It.IsAny<TChildEntity>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task IgnoresAggregationWithoutChildren()
            {
                var aggregation = new TAggregationEntity
                {
                    RowKey = "rowKey"
                };

                var activeChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different"
                };

                var recentChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different",
                    EndTime = Cursor
                };

                Table.SetupQuery(activeChildDifferentEntity, recentChildDifferentEntity);

                await Updater.UpdateAsync(aggregation, Cursor);

                Table
                    .Verify(
                        x => x.ReplaceAsync(It.IsAny<ITableEntity>()),
                        Times.Never());

                ChildUpdater
                    .Verify(
                        x => x.UpdateAsync(It.IsAny<TChildEntity>(), It.IsAny<DateTime>()),
                        Times.Never());
            }

            [Fact]
            public async Task DeactivatesAggregationWithoutRecentChildren()
            {
                var aggregation = new TAggregationEntity
                {
                    RowKey = "rowKey"
                };

                var activeChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different"
                };

                var recentChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different",
                    EndTime = Cursor
                };

                var oldChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor - EndMessageDelay
                };

                var olderChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor - EndMessageDelay - EndMessageDelay
                };

                Table.SetupQuery(activeChildDifferentEntity, recentChildDifferentEntity, oldChildSameEntity, olderChildSameEntity);

                ChildUpdater
                    .Setup(x => x.UpdateAsync(oldChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                Table
                    .Setup(x => x.ReplaceAsync(aggregation))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(aggregation, Cursor);

                Assert.Equal(oldChildSameEntity.EndTime, aggregation.EndTime);

                Table.Verify();
                ChildUpdater.Verify();
            }

            [Fact]
            public async Task DoesNotDeactivateAggregationWithActiveChildren()
            {
                var aggregation = new TAggregationEntity
                {
                    RowKey = "rowKey"
                };

                var activeChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different"
                };

                var recentChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different",
                    EndTime = Cursor
                };

                var oldChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor - EndMessageDelay
                };

                var activeChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey
                };

                Table.SetupQuery(
                    activeChildDifferentEntity,
                    recentChildDifferentEntity,
                    oldChildSameEntity,
                    activeChildSameEntity);

                ChildUpdater
                    .Setup(x => x.UpdateAsync(oldChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                ChildUpdater
                    .Setup(x => x.UpdateAsync(activeChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(aggregation, Cursor);

                Assert.True(aggregation.IsActive);

                Table
                    .Verify(
                        x => x.ReplaceAsync(aggregation),
                        Times.Never());

                ChildUpdater.Verify();
            }

            [Fact]
            public async Task DoesNotDeactivateAggregationWithRecentChildren()
            {
                var aggregation = new TAggregationEntity
                {
                    RowKey = "rowKey"
                };

                var activeChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different"
                };

                var recentChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different",
                    EndTime = Cursor
                };

                var oldChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor - EndMessageDelay
                };

                var recentChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor
                };

                Table.SetupQuery(
                    activeChildDifferentEntity,
                    recentChildDifferentEntity,
                    oldChildSameEntity,
                    recentChildSameEntity);

                ChildUpdater
                    .Setup(x => x.UpdateAsync(oldChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                ChildUpdater
                    .Setup(x => x.UpdateAsync(recentChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(aggregation, Cursor);

                Assert.True(aggregation.IsActive);

                Table
                    .Verify(
                        x => x.ReplaceAsync(aggregation),
                        Times.Never());

                ChildUpdater.Verify();
            }

            [Fact]
            public async Task DoesNotDeactivateAggregationWithActiveAndRecentChildren()
            {
                var aggregation = new TAggregationEntity
                {
                    RowKey = "rowKey"
                };

                var activeChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different"
                };

                var recentChildDifferentEntity = new TChildEntity
                {
                    ParentRowKey = "different",
                    EndTime = Cursor
                };

                var oldChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor - EndMessageDelay
                };

                var activeChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey
                };

                var recentChildSameEntity = new TChildEntity
                {
                    ParentRowKey = aggregation.RowKey,
                    EndTime = Cursor
                };

                Table.SetupQuery(
                    activeChildDifferentEntity,
                    recentChildDifferentEntity,
                    oldChildSameEntity,
                    activeChildSameEntity,
                    recentChildSameEntity);

                ChildUpdater
                    .Setup(x => x.UpdateAsync(oldChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                ChildUpdater
                    .Setup(x => x.UpdateAsync(activeChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                ChildUpdater
                    .Setup(x => x.UpdateAsync(recentChildSameEntity, Cursor))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await Updater.UpdateAsync(aggregation, Cursor);

                Assert.True(aggregation.IsActive);

                Table
                    .Verify(
                        x => x.ReplaceAsync(aggregation),
                        Times.Never());

                ChildUpdater.Verify();
            }
        }

        public class AggregationEntityUpdaterTest<TChildEntity, TAggregationEntity>
            where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>, new()
            where TAggregationEntity : ComponentAffectingEntity, new()
        {
            public static readonly DateTime Cursor = new DateTime(2018, 9, 13);
            public static readonly TimeSpan EndMessageDelay = TimeSpan.FromDays(1);

            public Mock<ITableWrapper> Table { get; }
            public Mock<IComponentAffectingEntityUpdater<TChildEntity>> ChildUpdater { get; }

            public AggregationEntityUpdater<TChildEntity, TAggregationEntity> Updater { get; }

            public AggregationEntityUpdaterTest()
            {
                Table = new Mock<ITableWrapper>();

                ChildUpdater = new Mock<IComponentAffectingEntityUpdater<TChildEntity>>();
                
                var config = new StatusAggregatorConfiguration
                {
                    EventEndDelayMinutes = (int)EndMessageDelay.TotalMinutes
                };

                Updater = new AggregationEntityUpdater<TChildEntity, TAggregationEntity>(
                    Table.Object,
                    ChildUpdater.Object,
                    config,
                    Mock.Of<ILogger<AggregationEntityUpdater<TChildEntity, TAggregationEntity>>>());
            }
        }
    }
}