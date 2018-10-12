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
    public class EditStatusMessageManualChangeHandlerTests
    {
        public class TheHandleMethod
        {
            private Mock<ITableWrapper> _table;
            private EditStatusMessageManualChangeHandler _handler;

            public TheHandleMethod()
            {
                _table = new Mock<ITableWrapper>();
                _handler = new EditStatusMessageManualChangeHandler(_table.Object);
            }

            [Fact]
            public async Task ThrowsArgumentExceptionIfMissingEvent()
            {
                var entity = new EditStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21), "message");

                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult<EventEntity>(null));

                await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(entity));
            }

            [Fact]
            public async Task ThrowsArgumentExceptionIfMissingMessage()
            {
                var entity = new EditStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21), "message");

                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
                var messageRowKey = MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp);

                var existingEntity =
                    new EventEntity(
                        entity.EventAffectedComponentPath,
                        entity.EventStartTime,
                        ComponentStatus.Up,
                        null);

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult(existingEntity));

                _table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(messageRowKey))
                    .Returns(Task.FromResult<MessageEntity>(null));

                await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(entity));
            }

            [Theory]
            [InlineData(MessageType.Manual)]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            public async Task EditsMessage(MessageType type)
            {
                var entity = new EditStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21), "message");

                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
                var messageRowKey = MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp);

                var existingEntity =
                    new EventEntity(
                        entity.EventAffectedComponentPath,
                        entity.EventStartTime,
                        ComponentStatus.Up,
                        null);

                _table
                    .Setup(x => x.RetrieveAsync<EventEntity>(eventRowKey))
                    .Returns(Task.FromResult(existingEntity));

                var existingMessage = new MessageEntity(
                    existingEntity, 
                    entity.MessageTimestamp, 
                    "old message", 
                    type);

                _table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(messageRowKey))
                    .Returns(Task.FromResult(existingMessage));

                _table
                    .Setup(x => x.ReplaceAsync(
                        It.Is<MessageEntity>(messageEntity =>
                            messageEntity.PartitionKey == MessageEntity.DefaultPartitionKey &&
                            messageEntity.RowKey == messageRowKey &&
                            messageEntity.ParentRowKey == eventRowKey &&
                            messageEntity.Time == existingMessage.Time &&
                            messageEntity.Contents == entity.MessageContents &&
                            messageEntity.Type == (int)MessageType.Manual
                        )))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await _handler.Handle(entity);

                _table.Verify();
            }
        }
    }
}