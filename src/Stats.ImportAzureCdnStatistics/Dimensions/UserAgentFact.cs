// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.ImportAzureCdnStatistics
{
    public class UserAgentFact
    {
        public UserAgentFact(string userAgent)
        {
            // trim userAgent
            if (!string.IsNullOrEmpty(userAgent) && userAgent.Length >= 500)
            {
                userAgent = userAgent.Substring(0, 499) + ")";
            }

            UserAgent = userAgent;
        }

        public int Id { get; set; }

        public string UserAgent { get; set; }
    }
}