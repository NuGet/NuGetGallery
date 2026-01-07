// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Incidents;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Subclass of <see cref="IncidentRegexParsingHandler"/> that expects <see cref="Incident"/>s are prefixed with "[ENVIRONMENT]".
    /// </summary>
    public abstract class EnvironmentPrefixIncidentRegexParsingHandler : IncidentRegexParsingHandler
    {
        public EnvironmentPrefixIncidentRegexParsingHandler(
            string subtitleRegEx,
            IEnumerable<IIncidentRegexParsingFilter> filters)
            : base(
                  PrependEnvironmentRegexGroup(subtitleRegEx),
                  filters)
        {
            if (!filters.Any(f => f is EnvironmentRegexParsingFilter))
            {
                throw new ArgumentException(
                    $"A {nameof(EnvironmentPrefixIncidentRegexParsingHandler)} must be run with an {nameof(EnvironmentRegexParsingFilter)}!", 
                    nameof(filters));
            }
        }

        private static string PrependEnvironmentRegexGroup(string subtitleRegEx)
        {
            return $@"\[(?<{EnvironmentRegexParsingFilter.EnvironmentGroupName}>.+)\] {subtitleRegEx}";
        }
    }
}
