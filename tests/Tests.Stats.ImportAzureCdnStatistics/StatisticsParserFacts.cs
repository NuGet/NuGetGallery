// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class StatisticsParserFacts
    {
        public class TheIsBlackListedMethod
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void DoesNotBlacklistEmptyUserAgentStrings(string userAgent)
            {
                Assert.False(StatisticsParser.IsBlackListed(userAgent));
            }

            [Theory]
            [InlineData("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)")]
            public void DoesBlacklistAppInsightsUserAgentStrings(string userAgent)
            {
                Assert.True(StatisticsParser.IsBlackListed(userAgent));
            }
        }
    }
}