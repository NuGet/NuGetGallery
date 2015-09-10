// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.ImportAzureCdnStatistics
{
    public class UserAgentFact
    {
        public UserAgentFact(string userAgent)
        {
            UserAgent = TrimUserAgent(userAgent);
        }

        public int Id { get; set; }

        public string UserAgent { get; set; }

        public static string TrimUserAgent(string userAgent)
        {
            // trim userAgent
            if (!string.IsNullOrEmpty(userAgent) && userAgent.Length >= 900)
            {
                return userAgent.Substring(0, 899) + ")";
            }
            return userAgent;
        }
    }
}