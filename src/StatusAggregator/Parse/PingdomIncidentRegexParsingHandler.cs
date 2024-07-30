// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Factory;

namespace StatusAggregator.Parse
{
    public class PingdomIncidentRegexParsingHandler : IncidentRegexParsingHandler
    {
        public const string CheckNameGroupName = "CheckName";
        public const string CheckUrlGroupName = "CheckUrl";
        private static string SubtitleRegEx = $@"Pingdom check '(?<{CheckNameGroupName}>.+)' is failing! '(?<{CheckUrlGroupName}>.+)' is DOWN!";

        private readonly ILogger<PingdomIncidentRegexParsingHandler> _logger;

        public PingdomIncidentRegexParsingHandler(
            IEnumerable<IIncidentRegexParsingFilter> filters,
            ILogger<PingdomIncidentRegexParsingHandler> logger)
            : base(SubtitleRegEx, filters)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var checkName = groups[CheckNameGroupName].Value;
            _logger.LogInformation("Check name is {CheckName}.", checkName);

            switch (checkName)
            {
                case "CDN DNS":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName);
                    break;
                case "CDN Global":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.GlobalRegionName);
                    break;
                case "CDN China":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName);
                    break;
                case "Gallery DNS":
                case "Gallery Home":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName);
                    break;
                case "Gallery USNC /":
                case "Gallery USNC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName, NuGetServiceComponentFactory.UsncInstanceName);
                    break;
                case "Gallery USSC /":
                case "Gallery USSC /Packages":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName, NuGetServiceComponentFactory.UsscInstanceName);
                    break;
                case "Gallery USNC /api/v2/Packages()":
                case "Gallery USNC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName, NuGetServiceComponentFactory.UsncInstanceName);
                    break;
                case "Gallery USSC /api/v2/Packages()":
                case "Gallery USSC /api/v2/package/NuGet.GalleryUptime/1.0.0":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName, NuGetServiceComponentFactory.UsscInstanceName);
                    break;
                case "Search USNC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName, NuGetServiceComponentFactory.UsncInstanceName);
                    break;
                case "Search USSC /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.GlobalRegionName, NuGetServiceComponentFactory.UsscInstanceName);
                    break;
                case "Search EA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName, NuGetServiceComponentFactory.EaInstanceName);
                    break;
                case "Search SEA /query":
                    affectedComponentPath = ComponentUtility.GetPath(
                        NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName, NuGetServiceComponentFactory.SeaInstanceName);
                    break;
                default:
                    return false;
            }

            return true;
        }

        public override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Degraded;
            return true;
        }
    }
}
