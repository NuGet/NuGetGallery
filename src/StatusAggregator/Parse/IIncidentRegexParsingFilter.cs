// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Incidents;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// An additional filter that can be applied to a <see cref="IncidentRegexParser"/>
    /// </summary>
    public interface IIncidentRegexParsingFilter
    {
        /// <summary>
        /// Returns whether or not an <see cref="IncidentRegexParser"/> should parse <paramref name="incident"/>.
        /// </summary>
        bool ShouldParse(Incident incident, GroupCollection groups);
    }
}
