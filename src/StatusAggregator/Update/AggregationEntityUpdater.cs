﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    /// <summary>
    /// Updates a <typeparamref name="TAggregationEntity"/> and its <typeparamref name="TChildEntity"/>s.
    /// </summary>
    public class AggregationEntityUpdater<TChildEntity, TAggregationEntity> 
        : IComponentAffectingEntityUpdater<TAggregationEntity>
        where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>, new()
        where TAggregationEntity : ComponentAffectingEntity
    {
        public readonly TimeSpan _groupEndDelay;

        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TChildEntity> _aggregatedEntityUpdater;

        private readonly ILogger<AggregationEntityUpdater<TChildEntity, TAggregationEntity>> _logger;

        public AggregationEntityUpdater(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TChildEntity> aggregatedEntityUpdater,
            StatusAggregatorConfiguration configuration,
            ILogger<AggregationEntityUpdater<TChildEntity, TAggregationEntity>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregatedEntityUpdater = aggregatedEntityUpdater 
                ?? throw new ArgumentNullException(nameof(aggregatedEntityUpdater));
            _groupEndDelay = TimeSpan.FromMinutes(configuration?.EventEndDelayMinutes 
                ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAsync(TAggregationEntity aggregationEntity, DateTime cursor)
        {
            aggregationEntity = aggregationEntity ?? throw new ArgumentNullException(nameof(aggregationEntity));
            _logger.LogInformation("Updating aggregation {AggregationRowKey} given cursor {Cursor}.", aggregationEntity.RowKey, cursor);

            if (!aggregationEntity.IsActive)
            {
                _logger.LogInformation("Aggregation {AggregationRowKey} is inactive, cannot update.", aggregationEntity.RowKey);
                return;
            }

            var hasActiveOrRecentChildren = false;
            var children = _table
                .GetChildEntities<TChildEntity, TAggregationEntity>(aggregationEntity)
                .ToList();

            if (children.Any())
            {
                _logger.LogInformation("Aggregation {AggregationRowKey} has {ChildrenCount} children. Updating each child.", aggregationEntity.RowKey, children.Count);
                foreach (var child in children)
                {
                    await _aggregatedEntityUpdater.UpdateAsync(child, cursor);

                    hasActiveOrRecentChildren =
                        hasActiveOrRecentChildren ||
                        child.IsActive ||
                        child.EndTime > cursor - _groupEndDelay;
                }
            }
            else
            {
                _logger.LogInformation("Aggregation {AggregationRowKey} has no children and must have been created manually, cannot update.", aggregationEntity.RowKey);
                return;
            }

            if (!hasActiveOrRecentChildren)
            {
                _logger.LogInformation("Deactivating aggregation {AggregationRowKey} because its children are inactive and too old.", aggregationEntity.RowKey);
                var lastEndTime = children.Max(i => i.EndTime.Value);
                aggregationEntity.EndTime = lastEndTime;

                await _table.ReplaceAsync(aggregationEntity);
            }
            else
            {
                _logger.LogInformation("Aggregation {AggregationRowKey} has active or recent children so it will not be deactivated.", aggregationEntity.RowKey);
            }
        }
    }
}
