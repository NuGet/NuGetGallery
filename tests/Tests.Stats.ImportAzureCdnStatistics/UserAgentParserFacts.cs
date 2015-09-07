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
            [InlineData("NuGet Command Line/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line", "1", "2", "3")]
            [InlineData("NuGet VS PowerShell Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS PowerShell Console", "1", "2", "3")]
            [InlineData("NuGet VS Packages Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS Packages Dialog - Solution", "1", "2", "3")]
            [InlineData("NuGet Add Package Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Add Package Dialog", "1", "2", "3")]
            [InlineData("NuGet Package Manager Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Manager Console", "1", "2", "3")]
            [InlineData("NuGet Visual Studio Extension/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Visual Studio Extension", "1", "2", "3")]
            [InlineData("Package-Installer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Package-Installer", "1", "2", "3")]
            [InlineData("NuGet Command Line/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line", "1", "2", "3")]
            [InlineData("WebMatrix 1.2.3/4.5.6 (Microsoft Windows NT 6.2.9200.0)", "WebMatrix", "1", "2", "3")]
            [InlineData("NuGet Package Explorer Metro/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Explorer Metro", "1", "2", "3")]
            [InlineData("NuGet Package Explorer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Explorer", "1", "2", "3")]
            [InlineData("JetBrains TeamCity 1.2.3 (Microsoft Windows NT 6.2.9200.0)", "JetBrains TeamCity", "1", "2", "3")]
            [InlineData("Nexus/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Sonatype Nexus", "1", "2", "3")]
            [InlineData("Artifactory/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "JFrog Artifactory", "1", "2", "3")]
            [InlineData("MyGet/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MyGet", "1", "2", "3")]
            [InlineData("ProGet/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Inedo ProGet", "1", "2", "3")]
            [InlineData("Paket/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Paket", "1", "2", "3")]
            [InlineData("Paket", "Paket", null, null, null)]
            [InlineData("Xamarin Studio/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Xamarin Studio", "1", "2", "3")]
            [InlineData("MonoDevelop/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MonoDevelop", "1", "2", "3")]
            [InlineData("MonoDevelop-Unity/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "MonoDevelop", "1", "2", "3")]
            [InlineData("NuGet Command Line/2.8.50320.36 (Microsoft Windows NT 6.1.7601 Service Pack 1)", "NuGet Command Line", "2", "8", "50320")]
            [InlineData("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "2", "8", "50926")]
            [InlineData("NuGet/3.0.0 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "3", "0", "0")]
            [InlineData("Microsoft_.NET_Development_Utility/1.2.3-t150812191208 (Windows 6.2.9200.0)", "DNX Utility", "1", "2", "3")]
            [InlineData("NuGet Client V3/3.0.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "0", "0")]
            [InlineData("NuGet Client V3/3.1.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "1", "0")]
            [InlineData("SharpDevelop/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "SharpDevelop", "1", "2", "3")]
            [InlineData("Mozilla/5.0 (Windows NT; Windows NT 6.2; en-US) WindowsPowerShell/5.0.9701.0", "Windows PowerShell", "5", "0", "9701")]
            [InlineData("Fiddler", "Fiddler", null, null, null)]
            [InlineData("curl/7.21.0 (x86_64-pc-linux-gnu) libcurl/7.21.0 OpenSSL/0.9.8o zlib/1.2.3.4 libidn/1.18", "curl", "7", "21", "0")]
            public void RecognizesCustomClients(string userAgent, string expectedClient, string expectedMajor, string expectedMinor, string expectedPatch)
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
