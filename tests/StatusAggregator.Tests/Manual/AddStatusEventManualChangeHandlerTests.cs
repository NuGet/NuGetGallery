// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;
using Xunit;

namespace StatusAggregator.Tests.Manual
{
    public class AddStatusEventManualChangeHandlerTests
    {
        public class TheHandleMethod
        {
            private Mock<ITableWrapper> _table;
            private AddStatusEventManualChangeHandler _handler;

            public TheHandleMethod()
            {
                _table = new Mock<ITableWrapper>();
                _handler = new AddStatusEventManualChangeHandler(_table.Object);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SavesMessageAndEvent(bool eventIsActive)
            {
                var entity = new AddStatusEventManualChangeEntity("path", ComponentStatus.Up, "message", eventIsActive)
                {
                    Timestamp = new DateTimeOffset(2018, 8, 21, 0, 0, 0, TimeSpan.Zero)
                };

                var time = entity.Timestamp.UtcDateTime;
                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, time);

                _table.Setup(x => x.InsertAsync(
                    It.Is<MessageEntity>(messageEntity => 
                        messageEntity.PartitionKey == MessageEntity.DefaultPartitionKey &&
                        messageEntity.RowKey == MessageEntity.GetRowKey(eventRowKey, time) &&
                        messageEntity.ParentRowKey == eventRowKey &&
                        messageEntity.Time == time &&
                        messageEntity.Contents == entity.MessageContents
                    )))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                _table.Setup(x => x.InsertAsync(
                    It.Is<EventEntity>(eventEntity =>
                        eventEntity.PartitionKey == EventEntity.DefaultPartitionKey &&
                        eventEntity.RowKey == eventRowKey &&
                        eventEntity.AffectedComponentPath == entity.EventAffectedComponentPath &&
                        eventEntity.AffectedComponentStatus == entity.EventAffectedComponentStatus &&
                        eventEntity.StartTime == time &&
                        eventEntity.EndTime == (entity.EventIsActive ? (DateTime?)null : time)
                    )))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await _handler.Handle(entity);

                _table.Verify();
            }
        }
    }
}