// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public abstract class StatisticsParser
    {
        private static readonly IList<string> _blackListedUserAgentPatterns = new List<string>();

        static StatisticsParser()
        {
            // Blacklist user agent patterns we whish to ignore
            RegisterBlacklistPatterns();
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

        private static void RegisterBlacklistPatterns()
        {
            // Ignore requests coming from AppInsights
            _blackListedUserAgentPatterns.Add("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)");
        }

        protected static string GetUserAgentValue(CdnLogEntry cdnLogEntry)
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

        protected static string GetCustomFieldValue(IDictionary<string, string> customFieldDictionary, string operation)
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