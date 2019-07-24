// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
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

        [RelevancyTheory]
        [MemberData(nameof(EnsureFirstResultsData))]
        public async Task EnsureFirstResults(string searchTerm, string[] expectedFirstResults)
        {
            var results = await SearchAsync(searchTerm, take: 10);

            Assert.True(results.Count > expectedFirstResults.Length);

            for (var i = 0; i < expectedFirstResults.Length; i++)
            {
                Assert.True(expectedFirstResults[i] == results[i], $"Expected result '{expectedFirstResults[i]}' at index #{i} for query '{searchTerm}'");
            }
        }

        public static IEnumerable<object[]> EnsureFirstResultsData()
        {
            // Test that common queries have the most frequently selected results at the top.
            // These results were determined using the "BrowserSearchPage" and "BrowserSearchSelection" metrics
            // on the Gallery's Application Insights telemetry.
            // TODO: Reduce exact match boost and update "json.net", "redis", and "csv" tests.
            // See: https://github.com/NuGet/NuGetGallery/issues/7330
            yield return new object[] { "newtonsoft.json", new[] { "newtonsoft.json" } };
            yield return new object[] { "newtonsoft", new[] { "newtonsoft.json" } };
            yield return new object[] { "json.net", new[] { "json.net", "newtonsoft.json" } };
            yield return new object[] { "json", new[] { "json", "newtonsoft.json" } };

            yield return new object[] { "tags:\"aws-sdk-v3\"", new[] { "awssdk.core", "awssdk.s3" } };

            yield return new object[] { "entityframework", new[] { "entityframework" } };
            yield return new object[] { "entity framework", new[] { "entityframework" } };
            yield return new object[] { "EntityFrameworkCore", new[] { "microsoft.entityframeworkcore" } };
            yield return new object[] { "microsoft.entityframeworkcore", new[] { "microsoft.entityframeworkcore" } };
            yield return new object[] { "mysql", new[] { "mysql.data" } };

            yield return new object[] { "microsoft.aspnetcore.app", new[] { "microsoft.aspnetcore.app" } };
            yield return new object[] { "microsoft.extensions.logging", new[] { "microsoft.extensions.logging" } };

            yield return new object[] { "xunit", new[] { "xunit" } };
            yield return new object[] { "nunit", new[] { "nunit" } };
            yield return new object[] { "dapper", new[] { "dapper" } };
            yield return new object[] { "log4net", new[] { "log4net" } };
            yield return new object[] { "automapper", new[] { "automapper" } };
            yield return new object[] { "csv", new[] { "csv", "csvhelper" } };
            yield return new object[] { "bootstrap", new[] { "bootstrap" } };
            yield return new object[] { "moq", new[] { "moq" } };
            yield return new object[] { "serilog", new[] { "serilog" } };
            yield return new object[] { "redis", new[] { "redis", "stackexchange.redis", "microsoft.extensions.caching.redis" } };
        }

        [RelevancyTheory]
        [MemberData(nameof(EnsureTopResultsData))]
        public async Task EnsureTopResults(string searchTerm, string[] expectedTopResults)
        {
            var results = await SearchAsync(searchTerm, take: 10);

            Assert.True(results.Count > expectedTopResults.Length);

            foreach (var expectedTopResult in expectedTopResults)
            {
                Assert.True(results.Contains(expectedTopResult), $"Expected result '{expectedTopResult}' for query '{searchTerm}'");
            }
        }

        public static IEnumerable<object[]> EnsureTopResultsData()
        {
            // The following were chosen arbitrarily without telemetry.
            yield return new object[] { "Microsoft.Extensions", new[] { "microsoft.extensions.logging", "microsoft.extensions.configuration", "microsoft.extensions.dependencyinjection" } };
            yield return new object[] { "mvc", new[] { "microsoft.aspnet.mvc", "microsoft.aspnetcore.mvc" } };
        }
    }
}
