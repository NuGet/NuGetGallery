// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Incidents
{
    /// <summary>
    /// Allows access to the NuGet incident API.
    /// </summary>
    public interface IIncidentApiClient
    {
        /// <summary>
        /// Returns a single incident by its ID.
        /// </summary>
        Task<Incident> GetIncident(string id);

        /// <summary>
        /// Returns all incidents that pass a query.
        /// </summary>
        Task<IEnumerable<Incident>> GetIncidents(string query);
    }
}
