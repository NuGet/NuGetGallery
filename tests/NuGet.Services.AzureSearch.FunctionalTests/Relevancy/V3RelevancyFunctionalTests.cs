// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class V3RelevancyFunctionalTests : NuGetSearchFunctionalTestBase
    {
        public V3RelevancyFunctionalTests(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture, testOutputHelper)
        {
        }

        [RelevancyFact]
        public async Task Json()
        {
            var results = await SearchAsync("json");

            Assert.True(results.Count > 2);
            Assert.Equal("json", results[0]);
            Assert.Equal("newtonsoft.json", results[1]);
        }

        [RelevancyFact]
        public async Task NewtonsoftJson()
        {
            var results = await SearchAsync("Newtonsoft.Json");

            Assert.NotEmpty(results);
            Assert.Equal("newtonsoft.json", results[0]);
        }

        [RelevancyFact]
        public async Task Log()
        {
            var results = await SearchAsync("Log");

            Assert.NotEmpty(results);
            Assert.Contains("log4net", results);

            // TODO: These should be on the first page!
            //Assert.Contains("nlog", results);
            //Assert.Contains("serilog", results);
            //Assert.Contains("microsoft.extensions.logging, results);
        }

        [RelevancyFact]
        public async Task EntityFrameworkCore()
        {
            var results = await SearchAsync("EntityFrameworkCore");

            Assert.Equal("microsoft.entityframeworkcore", results[0]);
        }

        [RelevancyFact]
        public async Task MicrosoftExtensions()
        {
            var results = await SearchAsync("Microsoft.Extensions");

            Assert.Contains("microsoft.extensions.logging", results);
            Assert.Contains("microsoft.extensions.configuration", results);
            Assert.Contains("microsoft.extensions.dependencyinjection", results);
        }

        [RelevancyFact]
        public async Task Mvc()
        {
            var results = await SearchAsync("mvc");

            Assert.NotEmpty(results);
            Assert.Equal("microsoft.aspnet.mvc", results[0]);
            Assert.Contains("microsoft.aspnetcore.mvc", results);
        }
    }
}
