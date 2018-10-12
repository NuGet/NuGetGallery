// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Incidents;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Parses <see cref="Incident"/>s and determines whether or not they should be used to determine the <see cref="ServiceStatus"/>.
    /// </summary>
    public interface IIncidentParser
    {
        /// <summary>
        /// Attempts to parse <paramref name="incident"/> into <paramref name="parsedIncident"/>.
        /// </summary>
        /// <param name="parsedIncident">
        /// A <see cref="ParsedIncident"/> that describes <paramref name="incident"/> or <c>null</c> if <paramref name="incident"/> could not be parsed.
        /// </param>
        /// <returns>
        /// <c>true</c> if this <see cref="IIncidentParser"/> can be parse <paramref name="incident"/> and <c>false</c> otherwise.
        /// </returns>
        bool TryParseIncident(Incident incident, out ParsedIncident parsedIncident);
    }
}
