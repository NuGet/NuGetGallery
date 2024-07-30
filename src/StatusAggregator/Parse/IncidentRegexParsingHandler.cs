// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public abstract class IncidentRegexParsingHandler : IIncidentRegexParsingHandler
    {
        public IncidentRegexParsingHandler(
            string regexPattern,
            IEnumerable<IIncidentRegexParsingFilter> filters)
        {
            RegexPattern = regexPattern ?? throw new ArgumentNullException(nameof(regexPattern));
            Filters = filters?.ToList() ?? throw new ArgumentNullException(nameof(filters));
        }

        public string RegexPattern { get; }
        public IReadOnlyCollection<IIncidentRegexParsingFilter> Filters { get; }

        public abstract bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath);
        public abstract bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus);
    }
}
