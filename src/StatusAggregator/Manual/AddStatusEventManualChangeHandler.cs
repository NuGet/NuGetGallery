// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    public class AddStatusEventManualChangeHandler : IManualStatusChangeHandler<AddStatusEventManualChangeEntity>
    {
        private readonly ITableWrapper _table;

        public AddStatusEventManualChangeHandler(
            ITableWrapper table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public async Task Handle(AddStatusEventManualChangeEntity entity)
        {
            var time = entity.Timestamp.UtcDateTime;

            var eventEntity = new EventEntity(
                entity.EventAffectedComponentPath,
                time,
                affectedComponentStatus: (ComponentStatus)entity.EventAffectedComponentStatus,
                endTime: entity.EventIsActive ? (DateTime?)null : time);

            var messageEntity = new MessageEntity(
                eventEntity,
                time,
                entity.MessageContents,
                MessageType.Manual);

            await _table.InsertAsync(messageEntity);
            await _table.InsertAsync(eventEntity);
        }
    }
}
