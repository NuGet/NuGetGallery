// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace NuGet.Services.CDNRedirect.Tests
{
    public class CdnRedirectBehaviorTests
    {
        [Fact]
        public async Task StatusIndex_ReturnsHealthPage()
        {
            using var app = await CreateApplicationAsync("https://cdn.example.test");
            using var client = app.GetTestClient();

            using var response = await client.GetAsync("/Status/Index");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Ok", content);
        }

        [Fact]
        public async Task Request_ReturnsFoundWithDestinationPathAndQuery()
        {
            using var app = await CreateApplicationAsync("https://cdn.example.test");
            using var client = app.GetTestClient();

            using var response = await client.GetAsync("/packages/foo.nupkg?download=1");

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Equal(
                "https://cdn.example.test/packages/foo.nupkg?download=1",
                response.Headers.Location?.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-valid-destination")]
        [InlineData("http://cdn.example.test")]
        [InlineData("https://cdn.example.test?query=value")]
        [InlineData("https://cdn.example.test#fragment")]
        public async Task InvalidDestination_PreventsApplicationStartup(string? redirectDestination)
        {
            using var app = CreateApplication(redirectDestination);

            await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync());
        }

        private static async Task<WebApplication> CreateApplicationAsync(string redirectDestination)
        {
            var app = CreateApplication(redirectDestination);
            await app.StartAsync();
            return app;
        }

        private static WebApplication CreateApplication(string? redirectDestination)
        {
            return Program.CreateApplication(
                Array.Empty<string>(),
                builder =>
                {
                    builder.WebHost.UseTestServer();
                    builder.Configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["RedirectDestination"] = redirectDestination
                        });
                });
        }
    }
}
