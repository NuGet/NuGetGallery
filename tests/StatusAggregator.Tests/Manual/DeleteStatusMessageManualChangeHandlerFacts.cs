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
    public class DeleteStatusMessageManualChangeHandlerFacts
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
            public async Task DeletesEvent()
            {
                var entity = new DeleteStatusMessageManualChangeEntity("path", new DateTime(2018, 8, 20), new DateTime(2018, 8, 21));

                var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
                var messageRowKey = MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp);

                _table
                    .Setup(x => x.DeleteAsync(MessageEntity.DefaultPartitionKey, messageRowKey))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                await _handler.Handle(entity);

                _table.Verify();
            }
        }
    }
}
