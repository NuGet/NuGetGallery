// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Table
{
    public static class TableWrapperExtensions
    {
        public static IQueryable<TEntity> GetActiveEntities<TEntity>(this ITableWrapper table)
            where TEntity : ComponentAffectingEntity, new()
        {
            return table
                .CreateQuery<TEntity>()
                .Where(e => e.IsActive);
        }

        public static IQueryable<TChild> GetChildEntities<TChild, TParent>(this ITableWrapper table, TParent entity)
            where TChild : ITableEntity, IChildEntity<TParent>, new()
            where TParent : ITableEntity
        {
            return table
                .CreateQuery<TChild>()
                .Where(e => e.ParentRowKey == entity.RowKey);
        }
    }
}
