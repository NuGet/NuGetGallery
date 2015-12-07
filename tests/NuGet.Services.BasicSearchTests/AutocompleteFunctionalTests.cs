// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.BasicSearchTests.Models;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    public class AutocompleteFunctionalTests
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
                var response = await app.Client.GetAsync(new AutocompleteBuilder { Query = "something else" }.RequestUri);
                var result = await response.Content.ReadAsAsync<AutocompleteResult>(Serialization.MediaTypeFormatters);

                // Assert
                Assert.Equal(0, result.TotalHits);
                Assert.Empty(result.Data);
            }
        }
    }
}