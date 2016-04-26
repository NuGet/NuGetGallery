// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.BasicSearchTests.Models;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class V2SearchSkipFunctionalTests
    {
        [Fact]
        public async Task CanReturnEmptyResult()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Newtonsoft.Json", "7.0.1")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder { Query = "something else" }.RequestUri);
                var s = await response.Content.ReadAsStringAsync();
                var result = await response.Content.ReadAsAsync<V2SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal(0, result.TotalHits);
                Assert.Empty(result.Data);
            }
        }
        
        [Fact(Skip = "The old V2 search service always includes prerelease versions, even if the parameter is set to false.")]
        public async Task SupportsPrereleaseParameter()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Antlr", "3.1.3.42154"),
                new PackageVersion("Antlr", "3.4.1.9004-pre"),
                new PackageVersion("angularjs", "1.2.0-RC1"),
                new PackageVersion("WebGrease", "1.6.0")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                string query = "Id:angularjs Id:Antlr Id:WebGrease";

                // Act
                var withPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = false }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.Equal("1.2.0-RC1", withPrerelease.GetPackageVersion("angularjs"));  // the only version available is prerelease
                Assert.Equal("3.4.1.9004-pre", withPrerelease.GetPackageVersion("Antlr")); // the latest version is prerelease
                Assert.Equal("1.6.0", withPrerelease.GetPackageVersion("WebGrease"));      // the only version available is non-prerelease

                Assert.False(withoutPrerelease.ContainsPackage("angularjs"));              // the only version available is prerelease and is therefore excluded
                Assert.Equal("3.1.3.42154", withoutPrerelease.GetPackageVersion("Antlr")); // this is the latest non-release version
                Assert.Equal("1.6.0", withoutPrerelease.GetPackageVersion("WebGrease"));   // the only version available is non-prerelease
            }
        }

        [Fact]
        public async Task SortsResultsByDownload()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("Newtonsoft.Json", "7.0.1", 10),
                new PackageVersion("EntityFramework", "6.1.3", 20),
                new PackageVersion("bootstrap", "3.3.6", 5)
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V2SearchBuilder().RequestUri);
                var result = await response.Content.ReadAsAsync<V2SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal("EntityFramework", result.Data[0].PackageRegistration.Id);
                Assert.Equal("Newtonsoft.Json", result.Data[1].PackageRegistration.Id);
                Assert.Equal("bootstrap", result.Data[2].PackageRegistration.Id);
            }
        }

        [Fact]
        public async Task LatestAndPrereleaseFiltersAreIgnoredTheWayTheOldV2ServiceDidThings()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("bootstrap", "3.3.5"),
                new PackageVersion("bootstrap", "4.0.0-alpha",  listed: false),
                new PackageVersion("bootstrap", "4.0.0-alpha2", listed: true),
                new PackageVersion("bootstrap", "3.3.6")
            };

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                string query = "Id:bootstrap";

                // Act
                var withPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = true, IgnoreFilter = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new V2SearchBuilder { Query = query, Prerelease = false, IgnoreFilter = true }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<V2SearchResult>();

                // Assert
                Assert.True(withPrerelease.ContainsPackage("bootstrap"));                       // bootstrap is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "3.3.5"));              // stable version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "3.3.6"));              // stable version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha"));        // prerelease version is in the results
                Assert.True(withPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha2"));       // prerelease version is in the results
                var prerelease1 = withPrerelease.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease1.Listed);                                               // unlisted version is in the results

                Assert.True(withoutPrerelease.ContainsPackage("bootstrap"));                    // bootstrap is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "3.3.5"));           // stable version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "3.3.6"));           // stable version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha"));     // prerelease version is in the results
                Assert.True(withoutPrerelease.ContainsPackage("bootstrap", "4.0.0-alpha2"));    // prerelease version is in the results
                var prerelease2 = withoutPrerelease.GetPackage("bootstrap", "4.0.0-alpha");
                Assert.False(prerelease2.Listed);                                               // unlisted version is in the results
            }
        }
    }
}