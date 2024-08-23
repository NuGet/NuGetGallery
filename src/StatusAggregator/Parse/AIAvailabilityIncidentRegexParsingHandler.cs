// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;

namespace StatusAggregator.Parse
{
    public class AIAvailabilityIncidentRegexParsingHandler : EnvironmentPrefixIncidentRegexParsingHandler
    {
        public const string TestGroupName = "Test";
        public const string AffectedComponentPathGroupName = "AffectedComponentPath";
        private static string SubtitleRegEx = $@"AI Availability test '(?<{TestGroupName}>.+)' is failing!( \((?<{AffectedComponentPathGroupName}>.+)\))?";

        private readonly ILogger<AIAvailabilityIncidentRegexParsingHandler> _logger;

        public AIAvailabilityIncidentRegexParsingHandler(
            IEnumerable<IIncidentRegexParsingFilter> filters,
            ILogger<AIAvailabilityIncidentRegexParsingHandler> logger)
            : base(SubtitleRegEx, filters)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            var test = groups[TestGroupName].Value;
            affectedComponentPath = groups[AffectedComponentPathGroupName].Value;
            var environment = groups[EnvironmentRegexParsingFilter.EnvironmentGroupName].Value;
            _logger.LogInformation("Test is named {Test} and affects {AffectedComponentPath} in the {Environment} environment.", test, affectedComponentPath, environment);
            return !string.IsNullOrEmpty(affectedComponentPath);
        }

        public override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Down;
            return true;
        }
    }
}
