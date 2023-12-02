// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Helpers
{
    public static class WarningTitleHelper
    {
        public static string GetWarningIconTitle(
            string version,
            PackageDeprecation deprecation,
            PackageVulnerabilitySeverity? maxVulnerabilitySeverity)
        {
            // We want a tooltip title for the warning icon, which concatenates deprecation and vulnerability information cleanly
            var deprecationTitle = "";
            if (deprecation != null)
            {
                deprecationTitle = GetDeprecationTitle(version, deprecation.Status);
            }

            if (maxVulnerabilitySeverity.HasValue)
            {
                var vulnerabilitiesTitle = GetVulnerabilityTitle(version, maxVulnerabilitySeverity.Value);
                return string.IsNullOrEmpty(deprecationTitle)
                    ? vulnerabilitiesTitle
                    : $"{deprecationTitle.TrimEnd('.')}; {vulnerabilitiesTitle}";
            }

            return string.IsNullOrEmpty(deprecationTitle) ? string.Empty : deprecationTitle;
        }

        public static string GetVulnerabilityTitle(string version, PackageVulnerabilitySeverity maxVulnerabilitySeverity)
        {
            var severity = Enum.GetName(typeof(PackageVulnerabilitySeverity), maxVulnerabilitySeverity)?.ToLowerInvariant() ?? "unknown";
            return $"{version} has at least one vulnerability with {severity} severity.";
        }

        public static string GetDeprecationTitle(string version, PackageDeprecationStatus status)
        {
            var deprecationTitle = version;
            var isLegacy = status.HasFlag(PackageDeprecationStatus.Legacy);
            var hasCriticalBugs = status.HasFlag(PackageDeprecationStatus.CriticalBugs);

            if (hasCriticalBugs)
            {
                if (isLegacy)
                {
                    deprecationTitle += " is deprecated because it is no longer maintained and has critical bugs";
                }
                else
                {
                    deprecationTitle += " is deprecated because it has critical bugs";
                }
            }
            else if (isLegacy)
            {
                deprecationTitle += " is deprecated because it is no longer maintained";
            }
            else
            {
                deprecationTitle += " is deprecated";
            }

            return $"{deprecationTitle}.";
        }
    }
}