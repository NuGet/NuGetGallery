// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Stats.AzureCdnLogs.Common;

namespace Stats.ParseAzureCdnLogs
{
    public class PackageStatisticsParser
    {
        private static readonly IList<string> _blackListedUserAgentPatterns = new List<string>();

        static PackageStatisticsParser()
        {
            // Blacklist user agent patterns we whish to ignore
            RegisterBlacklistPatterns();
        }

        private static void RegisterBlacklistPatterns()
        {
            // Ignore requests coming from AppInsights
            _blackListedUserAgentPatterns.Add("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)");
        }

        public static bool IsBlackListed(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return false;
            }

            foreach (var blacklistedUserAgentPattern in _blackListedUserAgentPatterns)
            {
                if (userAgent.IndexOf(blacklistedUserAgentPattern, StringComparison.Ordinal) == 0)
                {
                    return true;
                }
            }

            return false;
        }

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
                if (!IsBlackListed(statistic.UserAgent))
                {
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