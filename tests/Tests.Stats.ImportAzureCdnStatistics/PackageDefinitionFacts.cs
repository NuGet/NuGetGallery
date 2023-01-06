// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using Stats.ImportAzureCdnStatistics;
using Stats.LogInterpretation;
using Xunit;

namespace Tests.Stats.LogInterpretation
{
    public class PackageDefinitionFacts
    {
        [Theory]
        [InlineData("nuget.core", "1.7.0.1540", "http://localhost/packages/nuget.core.1.7.0.1540.nupkg")]
        [InlineData("nuget.core", "1.0.1-beta1", "http://localhost/packages/nuget.core.1.0.1-beta1.nupkg")]
        [InlineData("nuget.core", "1.0.1-beta1.1", "http://localhost/packages/nuget.core.1.0.1-beta1.1.nupkg")]
        [InlineData("nuget.core", "1.0.1", "http://localhost/packages/nuget.core.1.0.1.nupkg")]
        [InlineData("1", "1.0.0", "http://localhost/packages/1.1.0.0.nupkg")]
        [InlineData("dnx-mono", "1.0.0-beta7", "http://localhost/packages/dnx-mono.1.0.0-beta7.nupkg")]
        [InlineData("Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus", "6.0.1304", "http://localhost/packages/Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ServiceBus.6.0.1304.nupkg")]
        [InlineData("新包", "1.0.0", "http://localhost/packages/%E6%96%B0%E5%8C%85.1.0.0.nupkg")]
        [InlineData("microsoft.applicationinsights.dependencycollector", "2.4.1", "http://localhost/packages/microsoft.applicationinsights.dependencycollector%20.2.4.1.nupkg")]
        [InlineData("xunit", "2.4.0-beta.1.build3958", "http://localhost/packages/xunit.2.4.0-beta.1.build3958.nupkg")]
        [InlineData("5.0.0.0", "5.0.0", "http://localhost/packages/5.0.0.0.5.0.0.nupkg")]
        [InlineData("xunit.1", "2.4.1", "https://api.nuget.org/v3-flatcontainer/xunit.1/2.4.1/xunit.1.2.4.1.nupkg")]
        //[InlineData("ImisComplexIpart20.2.1.235-EA", "20.2.1.235-EA", "http://localhost/packages/ImisComplexIpart20.2.1.235-EA.20.2.1.235-EA.nupkg")]
        //[InlineData("runtime.tizen.4.0.0-armel.Microsoft.NETCore.App", "2.0.0-preview1-002111-00", "http://localhost/packages/runtime.tizen.4.0.0-armel.Microsoft.NETCore.App.2.0.0-preview1-002111-00.nupkg")]
        public void ExtractsPackageIdAndVersionFromRequestUrl(string expectedPackageId, string expectedPackageVersion, string requestUrl)
        {
            var packageDefinitions = PackageDefinition.FromRequestUrl(requestUrl);
            var packageDefinition = packageDefinitions.First();
            Assert.Equal(expectedPackageId, packageDefinition.PackageId);
            Assert.Equal(expectedPackageVersion, packageDefinition.PackageVersion);
        }

        [Fact]
        public void ReturnsNullWhenInvalidPackageRequestUrl()
        {
            var packageDefinition = PackageDefinition.FromRequestUrl("http://localhost/api/v3/index.json");
            Assert.Null(packageDefinition);
        }

        [Theory]
        [InlineData("5.9.1", "https://localhost/artifacts/win-x86-commandline/v5.9.1/nuget.exe")]
        [InlineData("5.8.0-preview.2", "https://localhost/artifacts/win-x86-commandline/v5.8.0-preview.2/nuget.exe")]
        [InlineData("3.5.0-beta2", "https://localhost/artifacts/win-x86-commandline/v3.5.0-beta2/nuget.exe")]
        [InlineData("latest", "https://localhost/artifacts/win-x86-commandline/latest/nuget.exe")]
        public void FromNuGetExeUrlExtractsNuGetExeVersionFromUrl(string expectedVersion, string requestUrl)
        {
            var packageDefinition = PackageDefinition.FromNuGetExeUrl(requestUrl);
            Assert.Equal("tool/nuget.exe", packageDefinition.PackageId);
            Assert.Equal(expectedVersion, packageDefinition.PackageVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("http://localhost/downloads/nuget.exe")]
        [InlineData("http://localhost/artifacts/win-x86-commandline/3.5.0/nuget.exe")]
        [InlineData("http://localhost/artifacts/win-x86-commandline/vlatest/nuget.exe")]
        [InlineData("http://localhost/artifacts/win-x86-commandline/v3.5.0/get.exe")]
        public void FromNuGetExeUrlReturnsNullWhenInvalidUrl(string requestUrl)
        {
            var packageDefinition = PackageDefinition.FromNuGetExeUrl(requestUrl);
            Assert.Null(packageDefinition);
        }

        [Fact(Skip = "Use this to test performance of the parsing algorithm")]
        public void TestParsingPerformanceOnFile()
        {
            var filePath = $"C:\\Users\\skofman\\Desktop\\allpackages.csv";
            var ambiguousResultPath = "ambiguousresult.txt";
            var wrongResult = "wrongresult.txt";

            List<string> ambiguousPackages = new List<string>();
            List<string> wrongPackages = new List<string>();

            string[] lines = File.ReadAllLines(filePath);

            Stopwatch watch = Stopwatch.StartNew();

            foreach (var line in lines)
            {
                string[] components = line.Split(',');
                if (components.Length != 2)
                {
                    continue;
                }

                string id = components[0];
                string version = NuGetVersion.Parse(components[1]).ToNormalizedString();

                string url = $"http://localhost/packages/{id}.{version}.nupkg";
                var result = PackageDefinition.FromRequestUrl(url);

                if (result.Count() > 1)
                {
                    ambiguousPackages.Add($"[{id}][{version}]");

                    if (!(result[0].PackageId == id && result[0].PackageVersion == version))
                    {
                        wrongPackages.Add($"[{id}][{version}]");
                    }
                }

                if (!result.Any())
                {
                    throw new ArgumentException($"[{id}][{version}]");
                }
            }

            watch.Stop();

            Console.WriteLine($"Pass took: {watch.Elapsed.TotalSeconds}");

            File.WriteAllLines(ambiguousResultPath, ambiguousPackages);
            File.WriteAllLines(wrongResult, wrongPackages);
        }
    }
}