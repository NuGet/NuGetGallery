// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class EditStatusMessageManualChangeHandler : IManualStatusChangeHandler<EditStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public EditStatusMessageManualChangeHandler(
            ITableWrapper table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(EditStatusMessageManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var eventEntity = await _table.RetrieveAsync<EventEntity>(eventRowKey);
            if (eventEntity == null)
            {
                throw new ArgumentException("Cannot edit a message for an event that does not exist.");
            }

            var messageEntity = await _table.RetrieveAsync<MessageEntity>(MessageEntity.GetRowKey(eventRowKey, entity.MessageTimestamp));
            if (messageEntity == null)
            {
                throw new ArgumentException("Cannot edit a message that does not exist.");
            }

            messageEntity.Contents = entity.MessageContents;
            messageEntity.Type = (int)MessageType.Manual;

            await _table.ReplaceAsync(messageEntity);
        }
    }
}
