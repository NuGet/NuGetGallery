// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class ToolStatisticsParserFacts
    {
        [Theory]
        [InlineData("http://dist.nuget.org/80DB16/nugetdist.blob.core.windows.net/artifacts/win-x86-commandline/v3.1.0-beta/nuget.exe", "win-x86-commandline", "v3.1.0-beta", "nuget.exe")]
        [InlineData("https://dist.nuget.org/80DB16/nugetdist.blob.core.windows.net/artifacts/win-x86-commandline/v3.1.0-beta/nuget.exe", "win-x86-commandline", "v3.1.0-beta", "nuget.exe")]
        [InlineData("https://dist.nuget.org/80DB16/nugetdist.blob.core.windows.net/artifacts/visualstudio-2015-vsix/v3.2.0-rc/NuGet.Tools.2015.vsix", "visualstudio-2015-vsix", "v3.2.0-rc", "NuGet.Tools.2015.vsix")]
        public void GetToolStatisticsFromRequestUrl(string requestUrl, string id, string version, string exe)
        {
            var toolInfo = ToolStatisticsParser.GetToolStatisticsFromRequestUrl(requestUrl, DateTime.UtcNow);

            Assert.Equal(id, toolInfo.ToolId);
            Assert.Equal(version, toolInfo.ToolVersion);
            Assert.Equal(exe, toolInfo.FileName);
            Assert.Equal(string.Join("/", id, version, exe), toolInfo.Path);
        }
    }
}