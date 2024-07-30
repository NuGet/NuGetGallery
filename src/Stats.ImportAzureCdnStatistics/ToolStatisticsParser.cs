// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class ToolStatisticsParser
        : StatisticsParser
    {
        public static ToolStatistics FromCdnLogEntry(CdnLogEntry logEntry)
        {
            var statistics = GetToolStatisticsFromRequestUrl(logEntry.RequestUrl, logEntry.EdgeServerTimeDelivered);
            if (statistics != null)
            {
                statistics.EdgeServerIpAddress = logEntry.EdgeServerIpAddress;
                statistics.UserAgent = GetUserAgentValue(logEntry);
            }
            return statistics;
        }

        public static ToolStatistics GetToolStatisticsFromRequestUrl(string requestUrl, DateTime edgeServerTimeDelivered)
        {
            // Filter out non-valid request paths
            var lowerCaseRequestPath = requestUrl.ToLowerInvariant();
            if (!lowerCaseRequestPath.EndsWith(".exe")
                && !lowerCaseRequestPath.EndsWith(".vsix"))
            {
                return null;
            }

            var matches = Regex.Matches(requestUrl, @"(http[s]?[:]//dist.nuget.org/[\w]*\/nugetdist.blob.core.windows.net/artifacts/)(?<toolId>[\w-]+)/(?<toolVersion>[a-zA-Z0-9.-]+)/(?<fileName>[\w.]+)");
            if (matches.Count == 1)
            {
                var match = matches[0];
                var statistics = new ToolStatistics();
                statistics.EdgeServerTimeDelivered = edgeServerTimeDelivered;

                statistics.ToolId = match.Groups["toolId"].Value.Trim();
                statistics.ToolVersion = match.Groups["toolVersion"].Value.Trim();
                statistics.FileName = match.Groups["fileName"].Value.Trim();
                statistics.Path = string.Join("/", statistics.ToolId, statistics.ToolVersion, statistics.FileName);

                return statistics;
            }
            return null;
        }
    }
}