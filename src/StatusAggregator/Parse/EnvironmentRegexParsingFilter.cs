// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Expects that the <see cref="Incident"/> contains a <see cref="Group"/> named <see cref="EnvironmentGroupName"/> with a whitelisted value.
    /// </summary>
    public class EnvironmentRegexParsingFilter : IIncidentRegexParsingFilter
    {
        public const string EnvironmentGroupName = "Environment";

        private IEnumerable<string> _environments { get; }

        private readonly ILogger<EnvironmentRegexParsingFilter> _logger;

        public EnvironmentRegexParsingFilter(
            StatusAggregatorConfiguration configuration,
            ILogger<EnvironmentRegexParsingFilter> logger)
        {
            _environments = configuration?.Environments ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool ShouldParse(Incident incident, GroupCollection groups)
        {
            var group = groups[EnvironmentGroupName];

            if (group.Success)
            {
                var groupValue = group.Value;
                _logger.LogInformation("Incident has environment of {Environment}, expecting one of {Environments}.", 
                    groupValue, string.Join(";", _environments));
                return _environments.Any(
                    e => string.Equals(groups[EnvironmentGroupName].Value, e, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _logger.LogInformation("Incident does not have an enviroment group, will not filter by environment.");
                return true;
            }
        }
    }
}
