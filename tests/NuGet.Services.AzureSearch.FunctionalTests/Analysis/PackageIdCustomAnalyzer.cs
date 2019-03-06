// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.FunctionalTests.Support;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class PackageIdCustomAnalyzerFacts : AzureIndexFunctionalTests
    {
        private const string AnalyzerName = "nuget_package_id_analyzer";

        public PackageIdCustomAnalyzerFacts(CommonFixture fixture)
            : base(fixture)
        {
        }

        [AnalysisTheory]
        [MemberData(nameof(ProducesExpectedTokensData))]
        public async Task ProducesExpectedTokens(string input, string[] expectedTokens)
        {
            var actualTokens = new HashSet<string>(await AnalyzeAsync(AnalyzerName, input));

            foreach (var expectedToken in expectedTokens)
            {
                Assert.Contains(expectedToken, actualTokens);
            }

            Assert.Equal(expectedTokens.Length, actualTokens.Count);
        }

        public static IEnumerable<object[]> ProducesExpectedTokensData()
        {
            var data = new Dictionary<string, string[]>
            {
                // Tokens should be lowercased
                { "hello", new[] { "hello"} },
                { "Hello", new[] { "hello" } },

                // Tokens should be split on special characters
                { "Foo.Bar", new[] { "foo", "bar" } },
                { "Foo-Bar", new[] { "foo", "bar" } },
                { "Foo,Bar", new[] { "foo", "bar" } },
                { "Foo;Bar", new[] { "foo", "bar" } },
                { "Foo:Bar", new[] { "foo", "bar" } },
                { "Foo'Bar", new[] { "foo", "bar" } },
                { "Foo*Bar", new[] { "foo", "bar" } },
                { "Foo#Bar", new[] { "foo", "bar" } },
                { "Foo!Bar", new[] { "foo", "bar" } },
                { "Foo~Bar", new[] { "foo", "bar" } },
                { "Foo+Bar", new[] { "foo", "bar" } },
                { "Foo(Bar", new[] { "foo", "bar" } },
                { "Foo)Bar", new[] { "foo", "bar" } },
                { "Foo[Bar", new[] { "foo", "bar" } },
                { "Foo]Bar", new[] { "foo", "bar" } },
                { "Foo{Bar", new[] { "foo", "bar" } },
                { "Foo}Bar", new[] { "foo", "bar" } },
                { "Foo_Bar", new[] { "foo", "bar" } },

                // Tokens should be also be split by non alphanumeric characters
                // and by upper casing. However, these splits should not consume the
                // original token
                { "Hello World", new[] { "hello world", "hello", "world" } },
                { "HelloWorld", new[] { "helloworld", "hello", "world" } },
                { "foo2bar", new[] { "foo2bar", "foo", "2", "bar" } },
                { "HTML", new[] { "html"} },
                { "HTMLThing", new[] { "htmlthing" } },
                { "HTMLThingA", new[] { "htmlthinga", "htmlthing", "a" } },
            };

            foreach (var datum in data)
            {
                yield return new object[] { datum.Key, datum.Value };
            }
        }
    }
}
