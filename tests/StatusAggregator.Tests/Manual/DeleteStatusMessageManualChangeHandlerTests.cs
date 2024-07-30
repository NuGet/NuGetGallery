// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;
using Xunit;

namespace StatusAggregator.Tests.Manual
{
    public class DeleteStatusMessageManualChangeHandlerTests
    {
        public class TheHandleMethod
        {
            private Mock<ITableWrapper> _table;
            private DeleteStatusMessageManualChangeHandler _handler;

            public TheHandleMethod()
            {
                _table = new Mock<ITableWrapper>();
                _handler = new DeleteStatusMessageManualChangeHandler(_table.Object);
            }

            [Fact]
            public async Task FailsIfMessageDoesNotExist()
            {
                var entity = new DeleteStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21));
                _table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(It.IsAny<string>()))
                    .ReturnsAsync((MessageEntity)null);

                await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(entity));
            }

            [Theory]
            [InlineData(MessageType.Manual)]
            [InlineData(MessageType.Start)]
            [InlineData(MessageType.End)]
            public async Task DeletesEvent(MessageType type)
            {
                var entity = new DeleteStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21));

                var eventEntity = new EventEntity(
                    entity.EventAffectedComponentPath,
                    entity.EventStartTime);

                var eventRowKey = eventEntity.RowKey;

                var messageEntity = new MessageEntity(
                    eventEntity,
                    entity.MessageTimestamp,
                    "someContents",
                    type);

                var messageRowKey = messageEntity.RowKey;

                _table
                    .Setup(x => x.RetrieveAsync<MessageEntity>(messageRowKey))
                    .ReturnsAsync(messageEntity);
                
                _table
                    .Setup(x => x.ReplaceAsync(It.Is<MessageEntity>(
                        message =>
                            message.PartitionKey == MessageEntity.DefaultPartitionKey &&
                            message.RowKey == messageRowKey &&
                            message.ParentRowKey == eventRowKey &&
                            message.Time == messageEntity.Time &&
                            message.Contents == string.Empty &&
                            message.Type == (int)MessageType.Manual)))
                    .Returns(Task.CompletedTask);

                await _handler.Handle(entity);

                _table.Verify();
            }
        }
    }
}