// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.BasicSearchTests.Models;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class StartupTests
    {
        [Fact]
        public async Task PrereleaseParameterIsObserved()
        {
            // Arrange
            var packages = new[]
            {
                new TestSupport.PackageVersion("Antlr", "3.1.3.42154"),
                new TestSupport.PackageVersion("Antlr", "3.4.1.9004-pre"),
                new TestSupport.PackageVersion("angularjs", "1.2.0-RC1"),
                new TestSupport.PackageVersion("WebGrease", "1.6.0")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                string query = "Id:angularjs Id:Antlr Id:WebGrease";

                // Act
                var withPrereleaseResponse = await app.Client.GetAsync(new QueryBuilder { Query = query, Prerelease = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<QueryResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new QueryBuilder { Query = query, Prerelease = false }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<QueryResult>();

                // Assert
                Assert.Equal("1.2.0-RC1", withPrerelease.GetPackageVersion("angularjs"));  // the only version available is prerelease
                Assert.Equal("3.4.1.9004-pre", withPrerelease.GetPackageVersion("Antlr")); // the latest version is prerelease
                Assert.Equal("1.6.0", withPrerelease.GetPackageVersion("WebGrease"));      // the only version available is non-prerelease

                Assert.False(withoutPrerelease.ContainsPackage("angularjs"));              // the only version available is prerelease and is therefore excluded
                Assert.Equal("3.1.3.42154", withoutPrerelease.GetPackageVersion("Antlr")); // this is the latest non-release version
                Assert.Equal("1.6.0", withoutPrerelease.GetPackageVersion("WebGrease"));   // the only version available is non-prerelease
            }
        }
    }
}
