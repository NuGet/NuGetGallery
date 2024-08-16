// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class DeleteStatusMessageManualChangeHandler : IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public DeleteStatusMessageManualChangeHandler(
            ITableWrapper table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(DeleteStatusMessageManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var messageEntity = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp));
            if (messageEntity == null)
            {
                throw new ArgumentException("Cannot delete a message that does not exist.");
            }

            messageEntity.Contents = "";
            messageEntity.Type = (int)MessageType.Manual;

            await _table.ReplaceAsync(messageEntity);
        }
    }
}
