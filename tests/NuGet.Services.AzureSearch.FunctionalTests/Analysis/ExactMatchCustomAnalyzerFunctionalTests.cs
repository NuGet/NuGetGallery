// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class ExactMatchCustomAnalyzerFacts : AzureSearchIndexFunctionalTestBase
    {
        private const string AnalyzerName = "nuget_exact_match_analyzer";

        public ExactMatchCustomAnalyzerFacts(CommonFixture fixture)
            : base(fixture)
        {
        }

        [AnalysisFact]
        public async Task LowercasesInput()
        {
            var tokens = await AnalyzeAsync(AnalyzerName, "Hello world. FooBarBaz 𠈓");

            var token = Assert.Single(tokens);
            Assert.Equal("hello world. foobarbaz 𠈓", token);
        }
    }
}
