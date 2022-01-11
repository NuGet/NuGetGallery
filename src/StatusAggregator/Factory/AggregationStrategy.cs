// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator.Factory
{
    public class AggregationStrategy<TChildEntity, TAggregationEntity>
        : IAggregationStrategy<TAggregationEntity>
        where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>, new()
        where TAggregationEntity : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IComponentAffectingEntityUpdater<TAggregationEntity> _aggregationUpdater;

        private readonly ILogger<AggregationStrategy<TChildEntity, TAggregationEntity>> _logger;

        public AggregationStrategy(
            ITableWrapper table,
            IComponentAffectingEntityUpdater<TAggregationEntity> aggregationUpdater,
            ILogger<AggregationStrategy<TChildEntity, TAggregationEntity>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationUpdater = aggregationUpdater ?? throw new ArgumentNullException(nameof(aggregationUpdater));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> CanBeAggregatedByAsync(ParsedIncident input, TAggregationEntity aggregationEntity)
        {
            _logger.LogInformation("Determining if entity can be linked to aggregation {AggregationRowKey}", aggregationEntity.RowKey);
            if (!_table.GetChildEntities<TChildEntity, TAggregationEntity>(aggregationEntity).ToList().Any())
            {
                // A manually created aggregation will have no children. We cannot use an aggregation that was manually created.
                // It is also possible that some bug or data issue has broken this aggregation. If that is the case, we cannot use it either.
                _logger.LogInformation("Cannot link entity to aggregation because it is not linked to any children.");
                return false;
            }

            // To guarantee that the aggregation reflects the latest information and is actually active, we must update it.
            await _aggregationUpdater.UpdateAsync(aggregationEntity, input.StartTime);
            if (!aggregationEntity.IsActive && input.IsActive)
            {
                _logger.LogInformation("Cannot link entity to aggregation because it has been deactivated and the incident has not been.");
                return false;
            }

            _logger.LogInformation("Entity can be linked to aggregation.");
            return true;
        }
    }
}
