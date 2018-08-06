// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StatusAggregator.Parse
{
    public class TrafficManagerEndpointStatusIncidentParser : EnvironmentPrefixIncidentParser
    {
        private const string DomainGroupName = "Domain";
        private const string TargetGroupName = "Target";
        private static string SubtitleRegEx = $"Traffic Manager for (?<{DomainGroupName}>.*) is reporting (?<{TargetGroupName}>.*) as not Online!";

        private readonly ILogger<TrafficManagerEndpointStatusIncidentParser> _logger;

        public TrafficManagerEndpointStatusIncidentParser(
            IEnumerable<IIncidentParsingFilter> filters,
            ILogger<TrafficManagerEndpointStatusIncidentParser> logger)
            : base(SubtitleRegEx, filters, logger)
        {
            _logger = logger;
        }

        protected override bool TryParseAffectedComponentPath(Incident incident, GroupCollection groups, out string affectedComponentPath)
        {
            affectedComponentPath = null;

            var domain = groups[DomainGroupName].Value;
            var target = groups[TargetGroupName].Value;
            var environment = groups[EnvironmentFilter.EnvironmentGroupName].Value;
            _logger.LogInformation("Domain is {Domain}, target is {Target}, environment is {Environment}.", domain, target, environment);

            if (EnvironmentToDomainToTargetToPath.TryGetValue(environment, out var domainToTargetToPath) &&
                domainToTargetToPath.TryGetValue(domain, out var targetToPath) &&
                targetToPath.TryGetValue(target, out var path))
            {
                affectedComponentPath = path;
            }

            return affectedComponentPath != null;
        }

        protected override bool TryParseAffectedComponentStatus(Incident incident, GroupCollection groups, out ComponentStatus affectedComponentStatus)
        {
            affectedComponentStatus = ComponentStatus.Down;
            return true;
        }

        private static readonly string GalleryUsncPath =
            ComponentUtility.GetPath(
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.GalleryName,
                NuGetServiceComponentFactory.UsncInstanceName);

        private static readonly string GalleryUsscPath =
            ComponentUtility.GetPath(
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.GalleryName,
                NuGetServiceComponentFactory.UsscInstanceName);

        private static readonly string RestoreV3GlobalPath =
            ComponentUtility.GetPath(
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.RestoreName,
                NuGetServiceComponentFactory.V3ProtocolName,
                NuGetServiceComponentFactory.GlobalRegionName);

        private static readonly string RestoreV3ChinaPath =
            ComponentUtility.GetPath(
                NuGetServiceComponentFactory.RootName,
                NuGetServiceComponentFactory.RestoreName,
                NuGetServiceComponentFactory.V3ProtocolName,
                NuGetServiceComponentFactory.ChinaRegionName);

        private static readonly IDictionary<string, IDictionary<string, string>> DevDomainToTargetToPath =
            new Dictionary<string, IDictionary<string, string>>
            {
                {
                    "devnugettest.trafficmanager.net",
                    new Dictionary<string, string>
                    {
                        {
                            "nuget-dev-use2-gallery.cloudapp.net",
                            GalleryUsncPath
                        },

                        {
                            "nuget-dev-ussc-gallery.cloudapp.net",
                            GalleryUsncPath
                        }
                    }
                },

                {
                    "nugetapidev.trafficmanager.net",
                    new Dictionary<string, string>
                    {
                        {
                            "az635243.vo.msecnd.net",
                            RestoreV3GlobalPath
                        },
                        {
                            "nugetdevcnredirect.trafficmanager.net",
                            RestoreV3ChinaPath
                        }
                    }
                }
            };

        private static readonly IDictionary<string, IDictionary<string, string>> IntDomainToTargetToPath =
            new Dictionary<string, IDictionary<string, string>>
            {
                {
                    "nuget-int-test-failover.trafficmanager.net",
                    new Dictionary<string, string>
                    {
                        {
                            "nuget-int-0-v2gallery.cloudapp.net",
                            GalleryUsncPath
                        },

                        {
                            "nuget-int-ussc-gallery.cloudapp.net",
                            GalleryUsncPath
                        }
                    }
                }
            };

        private static readonly IDictionary<string, IDictionary<string, string>> ProdDomainToTargetToPath =
            new Dictionary<string, IDictionary<string, string>>
            {
                {
                    "nuget-prod-v2gallery.trafficmanager.net",
                    new Dictionary<string, string>
                    {
                        {
                            "nuget-prod-0-v2gallery.cloudapp.net",
                            GalleryUsncPath
                        },

                        {
                            "nuget-prod-ussc-gallery.cloudapp.net",
                            GalleryUsncPath
                        }
                    }
                },

                {
                    "nugetapiprod.trafficmanager.net",
                    new Dictionary<string, string>
                    {
                        {
                            "az320820.vo.msecnd.net",
                            RestoreV3GlobalPath
                        },
                        {
                            "nugetprodcnredirect.trafficmanager.net",
                            RestoreV3ChinaPath
                        }
                    }
                }
            };

        private static readonly IDictionary<string, IDictionary<string, IDictionary<string, string>>> EnvironmentToDomainToTargetToPath = 
            new Dictionary<string, IDictionary<string, IDictionary<string, string>>>
            {
                { "dev", DevDomainToTargetToPath },
                { "test", DevDomainToTargetToPath },
                { "int", IntDomainToTargetToPath },
                { "prod", ProdDomainToTargetToPath }
            };
    }
}
