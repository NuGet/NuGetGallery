// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class AutocompleteRelevancyFunctionalTests : NuGetSearchFunctionalTestBase
    {
        public AutocompleteRelevancyFunctionalTests(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture, testOutputHelper)
        {
        }

        [RelevancyTheory]
        [MemberData(nameof(EnsureFirstResultsData))]
        public async Task EnsureFirstResults(string searchTerm, string[] expectedFirstResults)
        {
            var results = await AutocompleteAsync(searchTerm, take: 10);

            Assert.True(results.Count > expectedFirstResults.Length);

            for (var i = 0; i < expectedFirstResults.Length; i++)
            {
                Assert.True(expectedFirstResults[i] == results[i], $"Expected result '{expectedFirstResults[i]}' at index #{i} for query '{searchTerm}'");
            }
        }

        [RelevancyTheory]
        [MemberData(nameof(EnsureTopResultsData))]
        public async Task EnsureTopResults(string searchTerm, string[] expectedTopResults)
        {
            var results = await AutocompleteAsync(searchTerm, take: 10);

            Assert.True(results.Count > expectedTopResults.Length);

            foreach (var expectedTopResult in expectedTopResults)
            {
                Assert.True(results.Contains(expectedTopResult), $"Expected result '{expectedTopResult}' for query '{searchTerm}'");
            }
        }

        public static IEnumerable<object[]> EnsureFirstResultsData()
        {
            yield return new object[] { "aws", new[] { "awssdk.core" } };
            yield return new object[] { "log4", new[] { "log4net" } };
            yield return new object[] { "dap", new[] { "dapper" } };
            yield return new object[] { "json", new[] { "json" } };
            yield return new object[] { "jso", new[] { "newtonsoft.json" } };
            yield return new object[] { "entityframeworkcore.relational", new[] { "microsoft.entityframeworkcore.relational" } };
            yield return new object[] { "core.mvc.razor", new[] { "microsoft.aspnetcore.mvc.razor" } };
            yield return new object[] { "microsoft.aspnet.mvc", new[] { "microsoft.aspnet.mvc" } };
        }

        public static IEnumerable<object[]> EnsureTopResultsData()
        {
            yield return new object[] { "extensions.log", new[] { "microsoft.extensions.logging" } };
            yield return new object[] { "extensions.logging", new[] { "microsoft.extensions.logging" } };
            yield return new object[] { "microsoft.extensio", new[] { "microsoft.extensions.logging.abstractions" } };
            yield return new object[] { "depen", new[] { "microsoft.extensions.dependencyinjection" } };
            yield return new object[] { "ent", new[] { "entityframework", "microsoft.entityframeworkcore" } };
            yield return new object[] { "entity", new[] { "entityframework", "microsoft.entityframeworkcore" } };
            yield return new object[] { "json", new[] { "newtonsoft.json" } };
            yield return new object[] { "logging", new[] { "microsoft.extensions.logging" } };
            yield return new object[] { "aut", new[] { "autofac", "automapper" } };
            yield return new object[] { "mysql", new[] { "mysql.data", "mysqlconnector" } };
            yield return new object[] { "redi", new[] { "stackexchange.redis" } };
        }
    }
}
