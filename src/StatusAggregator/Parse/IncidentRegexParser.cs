// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Implementation of <see cref="IIncidentParser"/> that uses <see cref="Regex"/> to parse <see cref="Incident"/>s with.
    /// </summary>
    public class IncidentRegexParser : IIncidentParser
    {
        private readonly static TimeSpan MaxRegexExecutionTime = TimeSpan.FromSeconds(5);

        private readonly IIncidentRegexParsingHandler _handler;

        private readonly ILogger<IncidentRegexParser> _logger;
        
        public IncidentRegexParser(
            IIncidentRegexParsingHandler handler,
            ILogger<IncidentRegexParser> logger)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(_handler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryParseIncident(Incident incident, out ParsedIncident parsedIncident)
        {
            var title = incident.Title;
            _logger.LogInformation("Using parser {IncidentParserType} with pattern {RegExPattern} to parse incident with title {IncidentTitle}",
                GetType(), _handler.RegexPattern, title);
            parsedIncident = null;

            Match match = null;
            try
            {
                match = Regex.Match(title, _handler.RegexPattern, RegexOptions.None, MaxRegexExecutionTime);
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvents.RegexFailure, e, "Failed to parse incident using regex!");
                return false;
            }

            if (match == null)
            {
                // According to its documentation, Regex.Match shouldn't return null, but this if statement is in here as a precaution.
                _logger.LogError("Parsed incident using regex successfully, but was unable to get match information!");
                return false;
            }

            _logger.LogDebug("RegEx match result: {MatchResult}", match.Success);
            return match.Success && TryParseIncident(incident, match.Groups, out parsedIncident);
        }

        private bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;
            
            if (_handler.Filters.Any(f =>
                {
                    _logger.LogInformation("Filtering incident using filter {IncidentFilterType}", f.GetType());
                    var shouldParse = f.ShouldParse(incident, groups);
                    _logger.LogInformation("Filter returned {FilterResult}.", shouldParse);
                    return !shouldParse;
                }))
            {
                _logger.LogInformation("Incident failed at least one filter!");
                return false;
            }

            if (!_handler.TryParseAffectedComponentPath(incident, groups, out var affectedComponentPath))
            {
                _logger.LogInformation("Could not parse incident component path!");
                return false;
            }

            _logger.LogInformation("Parsed affected component path {AffectedComponentPath}.", affectedComponentPath);

            if (!_handler.TryParseAffectedComponentStatus(incident, groups, out var affectedComponentStatus))
            {
                _logger.LogInformation("Could not parse incident component status!");
                return false;
            }

            _logger.LogInformation("Parsed affected component status {AffectedComponentPath}.", affectedComponentStatus);

            parsedIncident = new ParsedIncident(incident, affectedComponentPath, affectedComponentStatus);
            return true;
        }
    }
}
