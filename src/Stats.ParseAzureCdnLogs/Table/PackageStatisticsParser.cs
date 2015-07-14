// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Stats.AzureCdnLogs.Common;

namespace Stats.ParseAzureCdnLogs
{
    public class PackageStatisticsParser
    {
        public static IReadOnlyCollection<PackageStatistics> FromCdnLogEntries(IReadOnlyCollection<CdnLogEntry> logEntries)
        {
            var packageStatistics = new List<PackageStatistics>();

            foreach (var cdnLogEntry in logEntries)
            {
                var packageDefinition = PackageDefinition.FromRequestUrl(cdnLogEntry.RequestUrl);

                if (packageDefinition == null)
                {
                    continue;
                }

                var statistic = new PackageStatistics();

                // combination of partition- and row-key correlates each statistic to a cdn raw log entry
                statistic.PartitionKey = cdnLogEntry.PartitionKey;
                statistic.RowKey = cdnLogEntry.RowKey;
                statistic.EdgeServerTimeDelivered = cdnLogEntry.EdgeServerTimeDelivered;

                statistic.PackageId = packageDefinition.PackageId;
                statistic.PackageVersion = packageDefinition.PackageVersion;

                var customFieldDictionary = CdnLogCustomFieldParser.Parse(cdnLogEntry.CustomField);
                statistic.Operation = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetOperation);
                statistic.DependentPackage = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetDependentPackage);
                statistic.ProjectGuids = GetCustomFieldValue(customFieldDictionary, NuGetCustomHeaders.NuGetProjectGuids);
                statistic.UserAgent = GetUserAgentValue(cdnLogEntry);

                // ignore blacklisted user agents
                if (!NuGetClientResolver.IsBlackListed(statistic.UserAgent))
                {
                    var clientInfo = NuGetClientResolver.FromUserAgent(statistic.UserAgent);
                    statistic.Client = clientInfo.Name;
                    statistic.ClientCategory = clientInfo.Category;
                    statistic.ClientMajorVersion = clientInfo.GetMajorVersion(statistic.UserAgent);
                    statistic.ClientMinorVersion = clientInfo.GetMinorVersion(statistic.UserAgent);
                    statistic.ClientPlatform = clientInfo.GetPlatform(statistic.UserAgent);

                    packageStatistics.Add(statistic);
                }
            }

            return packageStatistics;
        }

        private static string GetUserAgentValue(CdnLogEntry cdnLogEntry)
        {
            if (cdnLogEntry.UserAgent.StartsWith("\"") && cdnLogEntry.UserAgent.EndsWith("\""))
            {
                return cdnLogEntry.UserAgent.Substring(1, cdnLogEntry.UserAgent.Length - 2);
            }
            else
            {
                return cdnLogEntry.UserAgent;
            }
        }

        private static string GetCustomFieldValue(IDictionary<string, string> customFieldDictionary, string operation)
        {
            if (customFieldDictionary.ContainsKey(operation))
            {
                var value = customFieldDictionary[operation];
                if (!string.Equals("-", value))
                {
                    return value;
                }
            }
            return string.Empty;
        }
    }
}