using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class V3RelevancyFunctionalTests : NuGetSearchFunctionalTestBase
    {
        public V3RelevancyFunctionalTests(CommonFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task Json()
        {
            var results = await SearchAsync("json");

            Assert.True(results.Count > 2);
            Assert.Equal("json", results[0]);
            Assert.Equal("newtonsoft.json", results[1]);
        }

        [Fact]
        public async Task NewtonsoftJson()
        {
            var results = await SearchAsync("Newtonsoft.Json");

            Assert.NotEmpty(results);
            Assert.Equal("newtonsoft.json", results[0]);
        }

        [Fact]
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

        [Fact]
        public async Task EntityFrameworkCore()
        {
            var results = await SearchAsync("EntityFrameworkCore");

            // TODO: This should be on the first page!
            //Assert.Contains("microsoft.entityframeworkcore", results);
        }

        [Fact]
        public async Task MicrosoftExtensions()
        {
            var results = await SearchAsync("Microsoft.Extensions");

            // TODO: This should be on the first page!
            //Assert.Contains("microsoft.extensions.logging", results);
            //Assert.Contains("microsoft.extensions.configuration", results);
            //Assert.Contains("microsoft.extensions.dependencyinjection", results);
        }

        [Fact]
        public async Task Mvc()
        {
            var results = await SearchAsync("mvc");

            Assert.NotEmpty(results);
            Assert.Equal("microsoft.aspnet.mvc", results[0]);

            // TODO: This should be on the first page!
            // Assert.Contains("Microsoft.AspNetCore.Mvc", results);
        }
    }
}
