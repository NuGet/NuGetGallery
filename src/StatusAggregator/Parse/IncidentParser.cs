// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Abstract implementation of <see cref="IIncidentParser"/> that allows specifying a <see cref="Regex"/> to analyze <see cref="Incident"/>s with.
    /// </summary>
    public abstract class IncidentParser : IIncidentParser
    {
        private readonly static TimeSpan MaxRegexExecutionTime = TimeSpan.FromSeconds(5);

        private readonly string _regExPattern;

        private readonly IEnumerable<IIncidentParsingFilter> _filters;

        private readonly ILogger<IncidentParser> _logger;

        public IncidentParser(
            string regExPattern, 
            ILogger<IncidentParser> logger)
        {
            _regExPattern = regExPattern ?? throw new ArgumentNullException(nameof(regExPattern));
            _filters = Enumerable.Empty<IIncidentParsingFilter>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IncidentParser(
            string regExPattern, 
            IEnumerable<IIncidentParsingFilter> filters, 
            ILogger<IncidentParser> logger)
            : this(regExPattern, logger)
        {
            _filters = filters?.ToList() ?? throw new ArgumentNullException(nameof(filters));
        }

        public bool TryParseIncident(Incident incident, out ParsedIncident parsedIncident)
        {
            var title = incident.Title;

            using (_logger.Scope("Using parser {IncidentParserType} with pattern {RegExPattern} to parse incident with title {IncidentTitle}",
                GetType(), _regExPattern, title))
            {
                parsedIncident = null;

                Match match = null;
                try
                {
                    match = Regex.Match(title, _regExPattern, RegexOptions.None, MaxRegexExecutionTime);
                }
                catch (Exception e)
                {
                    _logger.LogError(LogEvents.RegexFailure, e, "Failed to parse incident using regex!");
                    return false;
                }

                if (match == null)
                {
                    _logger.LogError("Parsed incident using regex successfully, but was unable to get match information!");
                    return false;
                }

                _logger.LogInformation("RegEx match result: {MatchResult}", title, match.Success);
                return match.Success && TryParseIncident(incident, match.Groups, out parsedIncident);
            }
        }

        private bool TryParseIncident(Incident incident, GroupCollection groups, out ParsedIncident parsedIncident)
        {
            parsedIncident = null;
            
            if (_filters.Any(f =>
                {
                    using (_logger.Scope("Filtering incident using filter {IncidentFilterType}", f.GetType()))
                    {
                        var shouldParse = f.ShouldParse(incident, groups);
                        _logger.LogInformation("Filter returned {FilterResult}.", shouldParse);
                        return !shouldParse;
                    }
                }))
            {
                _logger.LogInformation("Incident failed at least one filter!");
                return false;
            }

            if (!TryParseAffectedComponentPath(incident, groups, out var affectedComponentPath))
            {
                _logger.LogInformation("Could not parse incident component path!");
                return false;
            }

            _logger.LogInformation("Parsed affected component path {AffectedComponentPath}.", affectedComponentPath);

            if (!TryParseAffectedComponentStatus(incident, groups, out var affectedComponentStatus))
            {
                _logger.LogInformation("Could not parse incident component status!");
                return false;
            }

            _logger.LogInformation("Parsed affected component status {AffectedComponentPath}.", affectedComponentStatus);

            parsedIncident = new ParsedIncident(incident, affectedComponentPath, affectedComponentStatus);
            return true;
        }

        /// <summary>
        /// Attempts to parse a <see cref="ParsedIncident.AffectedComponentPath"/> from <paramref name="incident"/>.
        /// </summary>
        /// <param name="affectedComponentPath">
        /// The <see cref="ParsedIncident.AffectedComponentPath"/> parsed from <paramref name="incident"/> or <c>null</c> if <paramref name="incident"/> could not be parsed.
        /// </param>
        /// <returns>
        /// <c>true</c> if a <see cref="ParsedIncident.AffectedComponentPath"/> can be parsed from <paramref name="incident"/> and <c>false</c> otherwise.
        /// </returns>
        protected abstract bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath);

        /// <summary>
        /// Attempts to parse a <see cref="ParsedIncident.AffectedComponentStatus"/> from <paramref name="incident"/>.
        /// </summary>
        /// <param name="affectedComponentStatus"></param>
        /// The <see cref="ParsedIncident.AffectedComponentStatus"/> parsed from <paramref name="incident"/> or <see cref="default(ComponentStatus)"/> if <paramref name="incident"/> could not be parsed.
        /// <returns>
        /// <c>true</c> if a <see cref="ParsedIncident.AffectedComponentStatus"/> can be parsed from <paramref name="incident"/> and <c>false</c> otherwise.
        /// </returns>
        protected abstract bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus);
    }
}
