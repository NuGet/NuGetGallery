// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Incidents;
using System.Collections.Generic;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Aggregates the result of multiple <see cref="IIncidentParser"/>s on <paramref name="incident"/>.
    /// </summary>
    public interface IAggregateIncidentParser
    {
        /// <summary>
        /// Runs multiple <see cref="IIncidentParser"/>'s <see cref="IIncidentParser.TryParseIncident(Incident, out ParsedIncident)"/> with <paramref name="incident"/> and aggregates their results.
        /// </summary>
        IEnumerable<ParsedIncident> ParseIncident(Incident incident);
    }
}
