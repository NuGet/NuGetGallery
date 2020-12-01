// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Stats.LogInterpretation;
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
            [InlineData("NuGet Command Line/2.8.50320.36 (Microsoft Windows NT 6.1.7601 Service Pack 1)", "NuGet Command Line", "2", "8", "50320")]
            [InlineData("NuGet xplat/3.4.0 (Microsoft Windows NT 6.2.9200.0)", "NuGet Cross-Platform Command Line", "3", "4", "0")]
            [InlineData("NuGet VS PowerShell Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS PowerShell Console", "1", "2", "3")]
            [InlineData("NuGet VS Packages Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS Packages Dialog - Solution", "1", "2", "3")]
            [InlineData("NuGet Add Package Dialog/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Add Package Dialog", "1", "2", "3")]
            [InlineData("NuGet Package Manager Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Package Manager Console", "1", "2", "3")]
            [InlineData("NuGet Visual Studio Extension/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Visual Studio Extension", "1", "2", "3")]
            [InlineData("Package-Installer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Package-Installer", "1", "2", "3")]
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
            [InlineData("NuGet Core/2.8.50926.663 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "2", "8", "50926")]
            [InlineData("NuGet/2.8.6", "NuGet", "2", "8", "6")]
            [InlineData("NuGet/3.0.0 (Microsoft Windows NT 6.3.9600.0)", "NuGet", "3", "0", "0")]
            [InlineData("Microsoft_.NET_Development_Utility/1.2.3-t150812191208 (Windows 6.2.9200.0)", "DNX Utility", "1", "2", "3")]
            [InlineData("NuGet Shim/3.0.51103.210 (Microsoft Windows NT 6.2.9200.0)", "NuGet Shim", "3", "0", "51103")]
            [InlineData("NuGet Client V3/3.0.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "0", "0")]
            [InlineData("NuGet Client V3/3.1.0.0 (Microsoft Windows NT 10.0.10240.0, VS Enterprise/14.0)", "NuGet Client V3", "3", "1", "0")]
            [InlineData("SharpDevelop/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "SharpDevelop", "1", "2", "3")]
            [InlineData("Mozilla/5.0 (Windows NT; Windows NT 6.2; en-US) WindowsPowerShell/5.0.9701.0", "Windows PowerShell", "5", "0", "9701")]
            [InlineData("Fiddler", "Fiddler", null, null, null)]
            [InlineData("curl/7.21.0 (x86_64-pc-linux-gnu) libcurl/7.21.0 OpenSSL/0.9.8o zlib/1.2.3.4 libidn/1.18", "curl", "7", "21", "0")]
            [InlineData("Java/1.7.0_51", "Java", "1", "7", "0")]
            [InlineData("NuGet Core/2.8.6", "NuGet", "2", "8", "6")]
            [InlineData("NuGet Test Client/3.3.0", "NuGet Test Client", "3", "3", "0")]
            [InlineData("dotPeek/102.0.20150521.130901 (Microsoft Windows NT 6.3.9600.0; NuGet/2.8.60318.667; Wave/2.0.0; dotPeek/1.4.20150521.130901)", "JetBrains dotPeek", "1", "4", "20150521")]
            [InlineData("ReSharperPlatformVs10/102.0.20150521.123255 (Microsoft Windows NT 6.1.7601 Service Pack 1; NuGet/2.8.60318.667; Wave/2.0.0; ReSharper/9.1.20150521.134223; dotTrace/6.1.20150521.132011)", "JetBrains ReSharper Platform VS2010", "102", "0", "20150521")]
            [InlineData("ReSharperPlatformVs11/102.0.20150408.145317 (Microsoft Windows NT 6.2.9200.0; NuGet/2.8.50926.602; Wave/2.0.0; ReSharper/9.1.20150408.155143)", "JetBrains ReSharper Platform VS2012", "102", "0", "20150408")]
            [InlineData("ReSharperPlatformVs12/102.0.20150721.105606 (Microsoft Windows NT 6.3.9600.0; NuGet/2.8.60318.667; Wave/2.0.0; ReSharper/9.1.20150721.141555; dotTrace/6.1.20150721.135729; dotMemory/4.3.20150721.134307)", "JetBrains ReSharper Platform VS2013", "102", "0", "20150721")]
            [InlineData("ReSharperPlatformVs14/102.0.20150408.145317 (Microsoft Windows NT 10.0.10074.0; NuGet/2.8.50926.602; Wave/2.0.0; ReSharper/9.1.20150408.155143)", "JetBrains ReSharper Platform VS2015", "102", "0", "20150408")]
            [InlineData("NuGet MSBuild Task/4.0.0 (Microsoft Windows 10.0.15063)", "NuGet MSBuild Task", "4", "0", "0")]
            [InlineData("NuGet .NET Core MSBuild Task/4.4.0 (Microsoft Windows 10.0.15063)", "NuGet .NET Core MSBuild Task", "4", "4", "0")]
            [InlineData("NuGet Desktop MSBuild Task/4.4.0 (Microsoft Windows 10.0.15063)", "NuGet Desktop MSBuild Task", "4", "4", "0")]
            [InlineData("Cake NuGet Client/4.3.0 (Microsoft Windows 10.0.15063)", "Cake NuGet Client", "4", "3", "0")]
            [InlineData("NuGet VS VSIX/4.3.0 (Microsoft Windows 10.0.15063)", "NuGet VS VSIX", "4", "3", "0")]
            [InlineData("NuGet+VS+VSIX/4.8.1+(Microsoft+Windows+NT+10.0.17134.0,+VS+Enterprise/15.0)", "NuGet VS VSIX", "4", "8", "1")]
            [InlineData("NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)", "NuGet Command Line", "4", "3", "0")]
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
