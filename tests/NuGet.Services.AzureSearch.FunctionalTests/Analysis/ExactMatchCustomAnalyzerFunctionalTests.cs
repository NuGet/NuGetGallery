// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.AzureSearch.FunctionalTests.Support;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class ExactMatchCustomAnalyzerFacts : AzureIndexFunctionalTests
    {
        private const string AnalyzerName = "nuget_exact_match_analyzer";

        public ExactMatchCustomAnalyzerFacts(CommonFixture fixture)
            : base(fixture)
        {
        }

        [AnalysisFact]
        public async Task LowercasesInput()
        {
            var tokens = await AnalyzeAsync(AnalyzerName, "Hello world. FooBarBaz");

            var token = Assert.Single(tokens);
            Assert.Equal("hello world. foobarbaz", token);
        }
    }
}
