// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class PackageIdCustomAnalyzerFacts : AzureSearchIndexFunctionalTestBase
    {
        private const string AnalyzerName = "nuget_package_id_analyzer";

        public PackageIdCustomAnalyzerFacts(CommonFixture fixture)
            : base(fixture)
        {
        }

        [AnalysisTheory]
        [MemberData(nameof(TokenizationData.LowercasesTokens), MemberType = typeof(TokenizationData))]
        [MemberData(nameof(TokenizationData.SplitsTokensOnSpecialCharactersAndLowercases), MemberType = typeof(TokenizationData))]
        [MemberData(nameof(TokenizationData.LowercasesAndAddsTokensOnCasingAndNonAlphaNumeric), MemberType = typeof(TokenizationData))]
        public async Task ProducesExpectedTokens(string input, string[] expectedTokens)
        {
            var actualTokens = new HashSet<string>(await AnalyzeAsync(AnalyzerName, input));

            foreach (var expectedToken in expectedTokens)
            {
                Assert.Contains(expectedToken, actualTokens);
            }

            Assert.Equal(expectedTokens.Length, actualTokens.Count);
        }
    }
}
