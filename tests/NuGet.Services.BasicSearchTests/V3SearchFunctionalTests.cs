// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.BasicSearchTests.Models;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class V3SearchFunctionalTests
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
                var response = await app.Client.GetAsync(new V3SearchBuilder { Query = "something else" }.RequestUri);
                var result = await response.Content.ReadAsAsync<V3SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal(0, result.TotalHits);
                Assert.Empty(result.Data);
            }
        }

        [Fact]
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
                var withPrereleaseResponse = await app.Client.GetAsync(new V3SearchBuilder { Query = query, Prerelease = true }.RequestUri);
                var withPrerelease = await withPrereleaseResponse.Content.ReadAsAsync<V3SearchResult>();

                var withoutPrereleaseResponse = await app.Client.GetAsync(new V3SearchBuilder { Query = query, Prerelease = false }.RequestUri);
                var withoutPrerelease = await withoutPrereleaseResponse.Content.ReadAsAsync<V3SearchResult>();

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
                var response = await app.Client.GetAsync(new V3SearchBuilder().RequestUri);
                var result = await response.Content.ReadAsAsync<V3SearchResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal("EntityFramework", result.Data[0].Id);
                Assert.Equal("Newtonsoft.Json", result.Data[1].Id);
                Assert.Equal("bootstrap", result.Data[2].Id);
            }
        }

        [Fact]
        public async Task ReturnsCorrectSchema()
        {
            // Arrange
            var packages = new[]
            {
                new PackageVersion("EntityFramework", "6.1.3-beta1", 30), // EntityFramework has all of the fields populated.
                new PackageVersion("EntityFramework", "6.1.2", 20),
                new PackageVersion("angularjs", "1.4.8", 10)              // AngularJS a) has multiple authors and b) has no summary.
            };
            var expectedPath = Path.Combine("Data", "V3SearchFunctionalTests.ReturnsCorrectSchema.json");
            var expectedJsonText = File.ReadAllText(expectedPath);
            var expected = JsonConvert.DeserializeObject<JObject>(expectedJsonText, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            });
            var before = DateTimeOffset.UtcNow;

            using (var app = await StartedWebApp.StartAsync(packages))
            {
                // Act
                var response = await app.Client.GetAsync(new V3SearchBuilder { Prerelease = true }.RequestUri);
                var actual = await response.Content.ReadAsAsync<JObject>();

                // Assert
                // validate the "lastReopen" property, which is not predictable
                var lastReopen = actual["lastReopen"].Value<DateTime>();
                Assert.True(lastReopen >= before);
                actual["lastReopen"] = expected["lastReopen"];

                // validate the rest of the payload
                Assert.Equal(expected, actual);
            }
        }
    }
}