// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Factory;

namespace StatusAggregator.Parse
{
    public class ValidationDurationIncidentRegexParsingHandler : EnvironmentPrefixIncidentRegexParsingHandler
    {
        private const string SubtitleRegEx = "Too many packages are stuck in the \"Validating\" state!";

        public ValidationDurationIncidentRegexParsingHandler(
            IEnumerable<IIncidentRegexParsingFilter> filters,
            ILogger<ValidationDurationIncidentRegexParsingHandler> logger)
            : base(SubtitleRegEx, filters)
        {
        }

        public override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName);
            return true;
        }

        public override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
