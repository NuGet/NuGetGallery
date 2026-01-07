// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class IncidentFactory : IComponentAffectingEntityFactory<IncidentEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IAggregationProvider<IncidentEntity, IncidentGroupEntity> _aggregationProvider;
        private readonly IAffectedComponentPathProvider<IncidentEntity> _pathProvider;

        private readonly ILogger<IncidentFactory> _logger;

        public IncidentFactory(
            ITableWrapper table,
            IAggregationProvider<IncidentEntity, IncidentGroupEntity> aggregationProvider,
            IAffectedComponentPathProvider<IncidentEntity> pathProvider,
            ILogger<IncidentFactory> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _aggregationProvider = aggregationProvider ?? throw new ArgumentNullException(nameof(aggregationProvider));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IncidentEntity> CreateAsync(ParsedIncident input)
        {
            var groupEntity = await _aggregationProvider.GetAsync(input);
            var affectedPath = _pathProvider.Get(input);
            _logger.LogInformation("Creating incident for parsed incident with path {AffectedComponentPath}.", affectedPath);
            var incidentEntity = new IncidentEntity(
                input.Id,
                groupEntity,
                affectedPath,
                input.AffectedComponentStatus,
                input.StartTime,
                input.EndTime);

            await _table.InsertOrReplaceAsync(incidentEntity);

            if (incidentEntity.AffectedComponentStatus > groupEntity.AffectedComponentStatus)
            {
                _logger.LogInformation("Incident {IncidentRowKey} has a greater severity than incident group {GroupRowKey} it was just linked to ({NewSeverity} > {OldSeverity}), updating group's severity.",
                    incidentEntity.RowKey, groupEntity.RowKey, (ComponentStatus)incidentEntity.AffectedComponentStatus, (ComponentStatus)groupEntity.AffectedComponentStatus);
                groupEntity.AffectedComponentStatus = incidentEntity.AffectedComponentStatus;
                await _table.ReplaceAsync(groupEntity);
            }

            return incidentEntity;
        }
    }
}
