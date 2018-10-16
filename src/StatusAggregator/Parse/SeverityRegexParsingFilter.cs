// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using System;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Expects that the severity of an <see cref="Incident"/> must be lower than a threshold.
    /// </summary>
    public class SeverityRegexParsingFilter : IIncidentRegexParsingFilter
    {
        private readonly int _maximumSeverity;

        private readonly ILogger<SeverityRegexParsingFilter> _logger;

        public SeverityRegexParsingFilter(
            StatusAggregatorConfiguration configuration,
            ILogger<SeverityRegexParsingFilter> logger)
        {
            _maximumSeverity = configuration?.MaximumSeverity ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            var actualSeverity = incident.Severity;
            _logger.LogInformation(
                "Filtering incident severity: severity is {IncidentSeverity}, must be less than or equal to {MaximumSeverity}",
                actualSeverity, _maximumSeverity);
            return actualSeverity <= _maximumSeverity;
        }
    }
}
