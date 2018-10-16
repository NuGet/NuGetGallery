// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class EditStatusEventManualChangeHandler : IManualStatusChangeHandler<EditStatusEventManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public EditStatusEventManualChangeHandler(
            ITableWrapper table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(EditStatusEventManualChangeEntity entity)
        {
            var eventRowKey = EventEntity.GetRowKey(entity.EventAffectedComponentPath, entity.EventStartTime);
            var eventEntity = await _table.RetrieveAsync<EventEntity>(eventRowKey);
            if (eventEntity == null)
            {
                throw new ArgumentException("Cannot edit an event that does not exist.");
            }

            eventEntity.AffectedComponentStatus = entity.EventAffectedComponentStatus;
            ManualStatusChangeUtility.UpdateEventIsActive(eventEntity, entity.EventIsActive, entity.Timestamp.UtcDateTime);

            await _table.ReplaceAsync(eventEntity);
        }
    }
}
