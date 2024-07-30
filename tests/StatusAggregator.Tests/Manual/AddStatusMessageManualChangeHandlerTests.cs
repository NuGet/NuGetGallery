// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace StatusAggregator.Tests.Manual
{
    public class AddStatusMessageManualChangeHandlerTests
    {
        public class TheHandleMethod
        {
            private Mock<ITableWrapper> _table;
            private AddStatusMessageManualChangeHandler _handler;

            public TheHandleMethod()
            {
                _table = new Mock<ITableWrapper>();
                _handler = new AddStatusMessageManualChangeHandler(_table.Object);
            }

            [Fact]
            public async Task DoesNotSaveIfEventIsMissing()
            {
                var entity = new AddStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 21), "message", false)
                {
                    Timestamp = new DateTimeOffset(2018, 8, 21, 0, 0, 0, TimeSpan.Zero)
                };

                var time = entity.Timestamp.UtcDateTime;
                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult<EventEntity>(null));

                _table
                    .Setup(x => x.InsertAsync(It.IsAny<MessageEntity>()))
                    .Returns(Task.CompletedTask);

                _table
                    .Setup(x => x.InsertAsync(It.IsAny<EventEntity>()))
                    .Returns(Task.CompletedTask);

                await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(entity));

                _table.Verify(x => x.InsertAsync(It.IsAny<MessageEntity>()), Times.Never());
                _table.Verify(x => x.InsertAsync(It.IsAny<EventEntity>()), Times.Never());
            }

            public static IEnumerable<object[]> SavesNewMessageAndUpdatesEventIfNecessary_Data
            {
                get
                {
                    foreach (var eventIsActive in new[] { false, true })
                    {
                        foreach (var shouldEventBeActive in new[] { false, true })
                        {
                            yield return new object[] { eventIsActive, shouldEventBeActive };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(SavesNewMessageAndUpdatesEventIfNecessary_Data))]
            public async Task SavesNewMessageAndUpdatesEventIfNecessary(bool eventIsActive, bool shouldEventBeActive)
            {
                var eventStartTime = new DateTime(2018, 8, 19);
                var entity = new AddStatusMessageManualChangeEntity("path", eventStartTime, "message", shouldEventBeActive)
                {
                    Timestamp = new DateTimeOffset(2018, 8, 21, 0, 0, 0, TimeSpan.Zero)
                };

                var time = entity.Timestamp.UtcDateTime;
                var existingEntity =
                    new EventEntity(
                        entity.EventAffectedComponentPath,
                        eventStartTime,
                        ComponentStatus.Up,
                        eventIsActive ? (DateTime?)null : new DateTime(2018, 8, 20));

                var eventRowKey = existingEntity.RowKey;
                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult(existingEntity));

                _table
                    .Setup(x => x.InsertAsync(It.IsAny<MessageEntity>()))
                    .Returns(Task.CompletedTask);

                _table
                    .Setup(x => x.ReplaceAsync(It.IsAny<EventEntity>()))
                    .Returns(Task.CompletedTask);

                await _handler.Handle(entity);

                _table
                    .Verify(
                        x => x.InsertAsync(
                            It.Is<MessageEntity>(message =>
                                message.PartitionKey == MessageEntity.DefaultPartitionKey &&
                                message.RowKey == MessageEntity.GetRowKey(eventRowKey, time) &&
                                message.ParentRowKey == eventRowKey &&
                                message.Time == time &&
                                message.Contents == entity.MessageContents
                            )),
                        Times.Once());

                var shouldEventBeUpdated = ManualStatusChangeUtility.ShouldEventBeActive(existingEntity, shouldEventBeActive, time);

                _table
                    .Verify(
                        x => x.InsertAsync(
                            It.Is<EventEntity>(eventEntity =>
                                eventEntity.PartitionKey == EventEntity.DefaultPartitionKey &&
                                eventEntity.RowKey == eventRowKey &&
                                eventEntity.AffectedComponentPath == existingEntity.AffectedComponentPath &&
                                eventEntity.AffectedComponentStatus == existingEntity.AffectedComponentStatus &&
                                eventEntity.StartTime == time
                            )),
                        shouldEventBeUpdated ? Times.Once() : Times.Never());
            }
        }
    }
}