// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class UserAgentParserFacts
    {
        public class TheFromPackageStatisticMethod
        {
            private readonly ITestOutputHelper _testOutputHelper;
            private readonly UserAgentParser _parser;

            public TheFromPackageStatisticMethod(ITestOutputHelper testOutputHelper)
            {
                _testOutputHelper = testOutputHelper;
                _parser = new UserAgentParser();
            }

            [Theory]
            [InlineData("NuGet Command Line/2.8.50926.602 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line", "2", "8", "50926")]
            public void RecognizesNuGetClients(string userAgent, string expectedClient, string expectedMajor, string expectedMinor, string expectedPatch)
            {
                var parsed = _parser.ParseUserAgent(userAgent);
                _testOutputHelper.WriteLine(parsed.ToString());

                Assert.Equal(expectedClient, parsed.Family);
                Assert.Equal(expectedMajor, parsed.Major);
                Assert.Equal(expectedMinor, parsed.Minor);
                Assert.Equal(expectedPatch, parsed.Patch);
            }
        }
    }
}
