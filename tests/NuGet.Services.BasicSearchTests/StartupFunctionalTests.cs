// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using NuGet.Services.BasicSearchTests.TestSupport;
using Xunit;

namespace NuGet.Services.BasicSearchTests
{
    [Collection(StartupTestCollection.Name)]
    public class StartupFunctionalTests
    {
        [Fact]
        public async Task Ready()
        {
            // Arrange
            using (var app = await StartedWebApp.StartAsync())
            {
                // Act
                var response = await app.Client.GetAsync("/");
                var content = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("READY", content);
            }
        }

        [Fact]
        public async Task InvalidEndpoint()
        {
            // Arrange
            using (var app = await StartedWebApp.StartAsync())
            {
                // Act
                var response = await app.Client.GetAsync("/invalid");
                var content = await response.Content.ReadAsStringAsync();

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.Equal("UNRECOGNIZED", content);
            }
        }
    }
}