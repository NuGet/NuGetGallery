// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class AddStatusMessageManualChangeHandler : IManualStatusChangeHandler<AddStatusMessageManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public AddStatusMessageManualChangeHandler(
            ITableWrapper table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(AddStatusMessageManualChangeEntity entity)
        {
            var time = entity.Timestamp.UtcDateTime;

            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var eventEntity = await _table.RetrieveAsync<EventEntity>(eventRowKey);
            if (eventEntity == null)
            {
                throw new ArgumentException("Cannot create a message for an event that does not exist.");
            }
            
            var messageEntity = new MessageEntity(eventEntity, time, entity.MessageContents, MessageType.Manual);

            await _table.InsertAsync(messageEntity);
            if (ManualStatusChangeUtility.UpdateEventIsActive(eventEntity, entity.EventIsActive, time))
            {
                await _table.ReplaceAsync(eventEntity);
            }
        }
    }
}
