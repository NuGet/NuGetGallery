// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class AggregationProvider<TChildEntity, TAggregationEntity>
        : IAggregationProvider<TChildEntity, TAggregationEntity>
        where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>, new()
        where TAggregationEntity : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IAffectedComponentPathProvider<TAggregationEntity> _aggregationPathProvider;
        private readonly IAggregationStrategy<TAggregationEntity> _strategy;
        private readonly IComponentAffectingEntityFactory<TAggregationEntity> _aggregationFactory;

        private readonly ILogger<AggregationProvider<TChildEntity, TAggregationEntity>> _logger;

        public AggregationProvider(
            ITableWrapper table,
            IAffectedComponentPathProvider<TAggregationEntity> aggregationPathProvider,
            IAggregationStrategy<TAggregationEntity> strategy,
            IComponentAffectingEntityFactory<TAggregationEntity> aggregationFactory,
            ILogger<AggregationProvider<TChildEntity, TAggregationEntity>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationPathProvider = aggregationPathProvider ?? throw new ArgumentNullException(nameof(aggregationPathProvider));
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _aggregationFactory = aggregationFactory ?? throw new ArgumentNullException(nameof(aggregationFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TAggregationEntity> GetAsync(ParsedIncident input)
        {
            TAggregationEntity aggregationEntity = null;

            var possiblePath = _aggregationPathProvider.Get(input);
            // Find an aggregation to link to
            var possibleAggregationsQuery = _table
                .CreateQuery<TAggregationEntity>()
                .Where(e =>
                    // The aggregation must affect the same path
                    e.AffectedComponentPath == possiblePath &&
                    // The aggregation must begin before or at the same time
                    e.StartTime <= input.StartTime);

            // The aggregation must cover the same time period
            if (input.IsActive)
            {
                // An active input can only be linked to an active aggregation
                possibleAggregationsQuery = possibleAggregationsQuery
                    .Where(e => e.IsActive);
            }
            else
            {
                // An inactive input can be linked to an active aggregation or an inactive aggregation that ends after it
                possibleAggregationsQuery = possibleAggregationsQuery
                    .Where(e =>
                        e.IsActive ||
                        e.EndTime >= input.EndTime);
            }

            var possibleAggregations = possibleAggregationsQuery
                .ToList();

            _logger.LogInformation("Found {AggregationCount} possible aggregations to link entity to with path {AffectedComponentPath}.", possibleAggregations.Count(), possiblePath);
            foreach (var possibleAggregation in possibleAggregations)
            {
                if (await _strategy.CanBeAggregatedByAsync(input, possibleAggregation))
                {
                    _logger.LogInformation("Linking entity to aggregation.");
                    aggregationEntity = possibleAggregation;
                    break;
                }
            }

            if (aggregationEntity == null)
            {
                _logger.LogInformation("Could not find existing aggregation to link to, creating new aggregation to link entity to.");
                aggregationEntity = await _aggregationFactory.CreateAsync(input);
                _logger.LogInformation("Created new aggregation {AggregationRowKey} to link entity to.", aggregationEntity.RowKey);
            }

            return aggregationEntity;
        }
    }
}
