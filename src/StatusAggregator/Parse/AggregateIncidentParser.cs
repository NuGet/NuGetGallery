// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using System;
using System.Collections.Generic;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Default implementation of <see cref="IAggregateIncidentParser"/> that returns all <see cref="ParsedIncident"/>s returned by its <see cref="IIncidentParser"/>s.
    /// </summary>
    public class AggregateIncidentParser : IAggregateIncidentParser
    {
        private readonly IEnumerable<IIncidentParser> _incidentParsers;

        private readonly ILogger<AggregateIncidentParser> _logger;

        public AggregateIncidentParser(
            IEnumerable<IIncidentParser> incidentParsers,
            ILogger<AggregateIncidentParser> logger)
        {
            _incidentParsers = incidentParsers ?? throw new ArgumentNullException(nameof(incidentParsers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<ParsedIncident> ParseIncident(Incident incident)
        {
            _logger.LogInformation("Parsing incident {IncidentId}", incident.Id);
            var parsedIncidents = new List<ParsedIncident>();
            foreach (var incidentParser in _incidentParsers)
            {
                if (incidentParser.TryParseIncident(incident, out var parsedIncident))
                {
                    parsedIncidents.Add(parsedIncident);
                }
            }

            return parsedIncidents;
        }
    }
}
