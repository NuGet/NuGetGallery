// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class ToolDimensionFacts
    {
        [Theory]
        [InlineData("win-x86-commandline", "v3.5.0", "NuGet.exe", "win-x86-commandline", "v3.5.0", "nuget.exe", true)] // Lowercase and uppercase are equal for filename
        [InlineData("win-x86-commandline", "v3.5.0", "nuget.exe", "win-x86-commandline", "V3.5.0", "nuget.exe", true)] // Lowercase and uppercase are equal for tool version
        [InlineData("WIN-x86-commandline", "v3.5.0", "nuget.exe", "win-x86-commandline", "v3.5.0", "nuget.exe", true)] // Lowercase and uppercase are equal for tool id
        [InlineData("win-x86-commandline", "v3.5.0", "NuGet.exe", "win-x86-commandline", "v3.5.0", "NuGet1.exe", false)] // different file name
        [InlineData("win-x86-commandline", "v3.5.0", "nuget.exe", "win-x86-commandline", "VV3.5.0", "nuget.exe", false)] // different tool version
        [InlineData("win-x86-commandline", "v3.5.0", "nuget.exe", "win-x64-commandline", "v3.5.0", "nuget.exe", false)] // different tool id
        public void ComparesToolsDimensionsCorrectly(string toolId1, string toolVersion1, string fileName1,
                                                    string toolId2, string toolVersion2, string fileName2, bool expectedResult)
        {
            var tool1 = new ToolDimension(toolId1, toolVersion1, fileName1);
            var tool2 = new ToolDimension(toolId2, toolVersion2, fileName2);

            //Act
            var actualResultEquals = tool1.Equals(tool2);
            var actualResultGetHashCode = tool1.GetHashCode() == tool2.GetHashCode();

            // Assert
            Assert.Equal(expectedResult, actualResultEquals);
            Assert.Equal(expectedResult, actualResultGetHashCode);
        }
    }
}
