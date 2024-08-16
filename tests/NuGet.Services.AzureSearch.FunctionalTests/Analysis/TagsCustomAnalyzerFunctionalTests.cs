// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class TagsCustomAnalyzerFunctionalTests : AzureSearchIndexFunctionalTestBase
    {
        private const string AnalyzerName = "nuget_tags_analyzer";

        public TagsCustomAnalyzerFunctionalTests(CommonFixture fixture)
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
            var tests = new List<object[]>();

            tests.AddRange(TokenizationData.LowercasesTokens);
            tests.AddRange(TokenizationData.TrimsTokens);
            tests.AddRange(TokenizationData.SplitsTokensAtLength300);
            tests.AddRange(TokenizationData.DoesNotSplitTokensOnSpecialCharacters);

            // The gallery database stores tags up to length 4,000.
            // Thus, the tags analyzer splits tokens into length 300 and removes duplicates, if any.
            tests.Add(new object[] { new string('a', 600), new[] { new string('a', 300) } });

            return tests;
        }
    }
}
