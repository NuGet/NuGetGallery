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
    public class EditStatusEventManualChangeHandlerTests
    {
        public class TheHandleMethod
        {
            private Mock<ITableWrapper> _table;
            private EditStatusEventManualChangeHandler _handler;

            public TheHandleMethod()
            {
                _table = new Mock<ITableWrapper>();
                _handler = new EditStatusEventManualChangeHandler(_table.Object);
            }

            [Fact]
            public async Task ThrowsArgumentExceptionIfMissingEvent()
            {
                var entity = new EditStatusEventManualChangeEntity("path", ComponentStatus.Degraded, new DateTime(2018, 8, 20), false)
                {
                    Timestamp = new DateTimeOffset(2018, 8, 21, 0, 0, 0, TimeSpan.Zero)
                };

                var time = entity.Timestamp.UtcDateTime;
                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult<EventEntity>(null));

                await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(entity));
            }

            public static IEnumerable<object[]> EditsEvent_Data
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
            [MemberData(nameof(EditsEvent_Data))]
            public async Task EditsEvent(bool eventIsActive, bool shouldEventBeActive)
            {
                var entity = new EditStatusEventManualChangeEntity("path", ComponentStatus.Degraded, new DateTime(2018, 8, 20), shouldEventBeActive)
                {
                    Timestamp = new DateTimeOffset(2018, 8, 21, 0, 0, 0, TimeSpan.Zero)
                };

                var time = entity.Timestamp.UtcDateTime;
                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);

                var existingEntity =
                    new EventEntity(
                        entity.EventAffectedComponentPath,
                        entity.EventStartTime,
                        ComponentStatus.Up,
                        eventIsActive ? (DateTime?)null : new DateTime(2018, 8, 19));

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult(existingEntity));

                var shouldUpdateEndTime = ManualStatusChangeUtility.ShouldEventBeActive(existingEntity, shouldEventBeActive, time);

                _table
                    .Setup(x => x.ReplaceAsync(
                        It.Is<EventEntity>(eventEntity =>
                            eventEntity.PartitionKey == EventEntity.DefaultPartitionKey &&
                            eventEntity.RowKey == eventRowKey &&
                            eventEntity.AffectedComponentPath == existingEntity.AffectedComponentPath &&
                            eventEntity.AffectedComponentStatus == entity.EventAffectedComponentStatus &&
                            eventEntity.StartTime == existingEntity.StartTime &&
                            eventEntity.EndTime == (shouldUpdateEndTime ? time : existingEntity.EndTime)
                        )))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await _handler.Handle(entity);

                _table.Verify();
            }
        }
    }
}