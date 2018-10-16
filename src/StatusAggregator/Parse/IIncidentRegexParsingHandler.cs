// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public interface IIncidentRegexParsingHandler
    {
        string RegexPattern { get; }
        IReadOnlyCollection<IIncidentRegexParsingFilter> Filters { get; }

        /// <summary>
        /// Attempts to parse a <see cref="ParsedIncident.AffectedComponentPath"/> from <paramref name="incident"/>.
        /// </summary>
        /// <param name="affectedComponentPath">
        /// The <see cref="ParsedIncident.AffectedComponentPath"/> parsed from <paramref name="incident"/> or <c>null</c> if <paramref name="incident"/> could not be parsed.
        /// </param>
        /// <returns>
        /// <c>true</c> if a <see cref="ParsedIncident.AffectedComponentPath"/> can be parsed from <paramref name="incident"/> and <c>false</c> otherwise.
        /// </returns>
        bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath);

        /// <summary>
        /// Attempts to parse a <see cref="ParsedIncident.AffectedComponentStatus"/> from <paramref name="incident"/>.
        /// </summary>
        /// <param name="affectedComponentStatus"></param>
        /// The <see cref="ParsedIncident.AffectedComponentStatus"/> parsed from <paramref name="incident"/> or <see cref="default(ComponentStatus)"/> if <paramref name="incident"/> could not be parsed.
        /// <returns>
        /// <c>true</c> if a <see cref="ParsedIncident.AffectedComponentStatus"/> can be parsed from <paramref name="incident"/> and <c>false</c> otherwise.
        /// </returns>
        bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus);
    }
}
