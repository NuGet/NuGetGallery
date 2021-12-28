// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StatusAggregator.Collector
{
    /// <summary>
    /// Fetches new <see cref="IncidentEntity"/>s using an <see cref="IIncidentApiClient"/>.
    /// </summary>
    public class IncidentEntityCollectorProcessor : IEntityCollectorProcessor
    {
        public const string IncidentsCollectorName = "incidents";
        
        private readonly IAggregateIncidentParser _aggregateIncidentParser;
        private readonly IIncidentApiClient _incidentApiClient;
        private readonly IComponentAffectingEntityFactory<IncidentEntity> _incidentFactory;
        private readonly ILogger<IncidentEntityCollectorProcessor> _logger;

        private readonly string _incidentApiTeamId;

        public IncidentEntityCollectorProcessor(
            IIncidentApiClient incidentApiClient,
            IAggregateIncidentParser aggregateIncidentParser,
            IComponentAffectingEntityFactory<IncidentEntity> incidentFactory,
            StatusAggregatorConfiguration configuration,
            ILogger<IncidentEntityCollectorProcessor> logger)
        {
            _incidentApiClient = incidentApiClient ?? throw new ArgumentNullException(nameof(incidentApiClient));
            _aggregateIncidentParser = aggregateIncidentParser ?? throw new ArgumentNullException(nameof(aggregateIncidentParser));
            _incidentFactory = incidentFactory ?? throw new ArgumentNullException(nameof(incidentFactory));
            _incidentApiTeamId = configuration?.TeamId ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => IncidentsCollectorName;

        public async Task<DateTime?> FetchSince(DateTime cursor)
        {
            _logger.LogInformation("Fetching all new incidents since {Cursor}.", cursor);

            var incidents = (await _incidentApiClient.GetIncidents(GetRecentIncidentsQuery(cursor)))
                // The incident API trims the milliseconds from any filter.
                // Therefore, a query asking for incidents newer than '2018-06-29T00:00:00.5Z' will return an incident from '2018-06-29T00:00:00.25Z'
                // We must perform a check on the CreateDate ourselves to verify that no old incidents are returned.
                .Where(i => i.CreateDate > cursor)
                .ToList();

            _logger.LogInformation("Found {IncidentCount} incidents to parse.", incidents.Count);
            var parsedIncidents = incidents
                .SelectMany(_aggregateIncidentParser.ParseIncident)
                .ToList();

            _logger.LogInformation("Parsed {ParsedIncidentCount} incidents.", parsedIncidents.Count);
            foreach (var parsedIncident in parsedIncidents.OrderBy(i => i.StartTime))
            {
                _logger.LogInformation(
                    "Creating incident for parsed incident with ID {ParsedIncidentID} affecting {ParsedIncidentPath} at {ParsedIncidentStartTime} with status {ParsedIncidentStatus}.",
                    parsedIncident.Id, parsedIncident.AffectedComponentPath, parsedIncident.StartTime, parsedIncident.AffectedComponentStatus);
                await _incidentFactory.CreateAsync(parsedIncident);
            }

            return incidents.Any() ? incidents.Max(i => i.CreateDate) : (DateTime?)null;
        }
        
        private string GetRecentIncidentsQuery(DateTime cursor)
        {
            var query = $"$filter=OwningTeamId eq '{_incidentApiTeamId}'";

            if (cursor != DateTime.MinValue)
            {
                query += $" and CreateDate gt datetime'{cursor.ToString("o")}'";
            }

            return query;
        }
    }
}