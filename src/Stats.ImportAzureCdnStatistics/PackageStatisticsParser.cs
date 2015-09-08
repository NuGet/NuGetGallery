// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageStatisticsParser
        : StatisticsParser
    {
        public static IReadOnlyCollection<PackageStatistics> FromCdnLogEntries(IReadOnlyCollection<CdnLogEntry> logEntries)
        {
            var packageStatistics = new List<PackageStatistics>();

            foreach (var cdnLogEntry in logEntries)
            {
                var statistic = FromCdnLogEntry(cdnLogEntry);
                if (statistic != null)
                {
                    packageStatistics.Add(statistic);
                }
            }

            return packageStatistics;
        }

        public static PackageStatistics FromCdnLogEntry(CdnLogEntry cdnLogEntry)
        {
            var packageDefinition = PackageDefinition.FromRequestUrl(cdnLogEntry.RequestUrl);

            if (packageDefinition == null)
            {
                return null;
            }

            var statistic = new PackageStatistics();
            statistic.EdgeServerTimeDelivered = cdnLogEntry.EdgeServerTimeDelivered;
            statistic.PackageId = packageDefinition.PackageId;
            statistic.PackageVersion = packageDefinition.PackageVersion;

            var customFieldDictionary = CdnLogCustomFieldParser.Parse(cdnLogEntry.CustomField);
            statistic.Operation = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetOperation);
            statistic.DependentPackage = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetDependentPackage);
            statistic.ProjectGuids = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetProjectGuids);
            statistic.UserAgent = GetUserAgentValue(cdnLogEntry);
            statistic.EdgeServerIpAddress = cdnLogEntry.EdgeServerIpAddress;

            // ignore blacklisted user agents
            if (!IsBlackListed(statistic.UserAgent))
            {
                return statistic;
            }
            return null;
        }
    }
}