// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Subclass of <see cref="IncidentParser"/> that expects <see cref="Incident"/>s are prefixed with "[ENVIRONMENT]".
    /// </summary>
    public abstract class EnvironmentPrefixIncidentParser : IncidentParser
    {
        public EnvironmentPrefixIncidentParser(
            string subtitleRegEx, 
            IEnumerable<IIncidentParsingFilter> filters,
            ILogger<EnvironmentPrefixIncidentParser> logger)
            : base(GetRegEx(subtitleRegEx), filters, logger)
        {
            if (!filters.Any(f => f is EnvironmentFilter))
            {
                throw new ArgumentException($"A {nameof(EnvironmentPrefixIncidentParser)} must be run with an {nameof(EnvironmentFilter)}!", nameof(filters));
            }
        }

        private static string GetRegEx(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentFilter.EnvironmentGroupName}>.*)\] {subtitleRegEx}";
        }
    }
}
