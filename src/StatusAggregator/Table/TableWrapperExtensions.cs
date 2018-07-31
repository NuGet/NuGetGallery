// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator.Table
{
    public static class TableWrapperExtensions
    {
        public static IQueryable<EventEntity> GetActiveEvents(this ITableWrapper table)
        {
            return table
                .CreateQuery<EventEntity>()
                .Where(e => e.PartitionKey == EventEntity.DefaultPartitionKey && e.IsActive);
        }

        public static IQueryable<IncidentEntity> GetIncidentsLinkedToEvent(this ITableWrapper table, EventEntity eventEntity)
        {
            return table
                .CreateQuery<IncidentEntity>()
                .Where(i =>
                    i.PartitionKey == IncidentEntity.DefaultPartitionKey &&
                    i.IsLinkedToEvent &&
                    i.EventRowKey == eventEntity.RowKey);
        }

        public static IQueryable<MessageEntity> GetMessagesLinkedToEvent(this ITableWrapper table, EventEntity eventEntity)
        {
            return table
                .CreateQuery<MessageEntity>()
                .Where(m =>
                    m.PartitionKey == MessageEntity.DefaultPartitionKey &&
                    m.EventRowKey == eventEntity.RowKey);
        }
    }
}
