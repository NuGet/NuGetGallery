// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator.Update
{
    public class IncidentUpdater : IComponentAffectingEntityUpdater<IncidentEntity>
    {
        private readonly ITableWrapper _table;
        private readonly IIncidentApiClient _incidentApiClient;
        private readonly ILogger<IncidentUpdater> _logger;

        public IncidentUpdater(
            ITableWrapper table,
            IIncidentApiClient incidentApiClient,
            ILogger<IncidentUpdater> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _incidentApiClient = incidentApiClient ?? throw new ArgumentNullException(nameof(incidentApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task UpdateAsync(IncidentEntity entity, DateTime cursor)
        {
            using (_logger.Scope("Updating incident with ID {IncidentApiId}.", entity.IncidentApiId))
            {
                if (!entity.IsActive)
                {
                    return;
                }

                var activeIncident = await _incidentApiClient.GetIncident(entity.IncidentApiId);
                var endTime = activeIncident.MitigationData?.Date;

                if (endTime != null)
                {
                    entity.EndTime = endTime;
                    _logger.LogInformation("Updated mitigation time of active incident to {MitigationTime}.", entity.EndTime);
                    await _table.ReplaceAsync(entity);
                }
            }
        }
    }
}
