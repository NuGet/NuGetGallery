// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ParseAzureCdnLogs;
using Xunit;

namespace Tests.Stats.ParseAzureCdnLogs
{
    public class NuGetClientInfoFacts
    {
        public class TheGetMajorVersionMethod
        {
            [Theory]
            [InlineData("NuGet Command Line/2.8.50926.602 (Microsoft Windows NT 6.2.9200.0)", 2)]
            [InlineData("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", 2)]
            [InlineData("NuGet Core/1.5 (Microsoft Windows NT 6.3.9600.0)", 1)]
            [InlineData("NuGet Core/1 (Microsoft Windows NT 6.3.9600.0)", 1)]
            public void ProperlyExtractsMajorVersionNumberForNuGetClients(string userAgent, int expectedMajorVersion)
            {
                var clientInfo = new NuGetClientInfo("NuGet VS PowerShell Console", "NuGet");
                var majorVersion = clientInfo.GetMajorVersion(userAgent);

                Assert.Equal(expectedMajorVersion, majorVersion);
            }
        }

        public class TheGetMinorVersionMethod
        {

            [Theory]
            [InlineData("NuGet Command Line/2.8.50926.602 (Microsoft Windows NT 6.2.9200.0)", 8)]
            [InlineData("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", 8)]
            [InlineData("NuGet Core/1.5 (Microsoft Windows NT 6.3.9600.0)", 5)]
            [InlineData("NuGet Core/1 (Microsoft Windows NT 6.3.9600.0)", 0)]
            public void ProperlyExtractsMinorVersionNumberForNuGetClients(string userAgent, int expectedMinorVersion)
            {
                var clientInfo = new NuGetClientInfo("NuGet VS PowerShell Console", "NuGet");
                var minorVersion = clientInfo.GetMinorVersion(userAgent);

                Assert.Equal(expectedMinorVersion, minorVersion);
            }
        }

        public class TheGetPlatformMethod
        {
            [Theory]
            [InlineData("NuGet Command Line/2.8.50926.602 (Microsoft Windows NT 6.2.9200.0)", "Microsoft Windows NT 6.2.9200.0")]
            [InlineData("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", "Microsoft Windows NT 6.3.9600.0")]
            [InlineData("NuGet Core/1.5 (Microsoft Windows NT 6.3.9600.0)", "Microsoft Windows NT 6.3.9600.0")]
            [InlineData("NuGet Core/1 (Microsoft Windows NT 6.3.9600.0)", "Microsoft Windows NT 6.3.9600.0")]
            public void ProperlyExtractsPlatformForNuGetClients(string userAgent, string expectedPlatformString)
            {
                var clientInfo = new NuGetClientInfo("NuGet VS PowerShell Console", "NuGet");
                var actualPlatform = clientInfo.GetPlatform(userAgent);

                Assert.Equal(expectedPlatformString, actualPlatform);
            }
        }
    }
}