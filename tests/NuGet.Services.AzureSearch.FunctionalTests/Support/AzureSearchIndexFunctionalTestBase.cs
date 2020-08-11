// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    /// <summary>
    /// Base for functional tests that use Azure Search index APIs.
    /// See: https://docs.microsoft.com/en-us/rest/api/searchservice/index-operations
    /// </summary>
    public class AzureSearchIndexFunctionalTestBase : BaseFunctionalTests, IClassFixture<CommonFixture>
    {
        public AzureSearchIndexFunctionalTestBase(CommonFixture fixture)
            : base(fixture.AzureSearchConfiguration.TestSettings.AzureSearchIndexUrl)
        {
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            Client.DefaultRequestHeaders.Add("Api-Key", Fixture.AzureSearchConfiguration.TestSettings.AzureSearchIndexAdminApiKey);
        }

        protected CommonFixture Fixture { get; private set; }

        protected async Task<IReadOnlyList<string>> AnalyzeAsync(string analyzer, string text)
        {
            var jsonContent = JsonConvert.SerializeObject(new
            {
                analyzer,
                text
            });

            var index = Fixture.AzureSearchConfiguration.TestSettings.AzureSearchIndexName;
            var requestUri = $"/indexes/{index}/analyze?api-version=2017-11-11";
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await Client.PostAsync(requestUri, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<AnalyzeResult>(json);

            return result?.Tokens.Select(t => t.Token).ToList();
        }

        private class AnalyzeResult
        {
            public IReadOnlyList<AnalyzeResultToken> Tokens { get; set; }
        }

        private class AnalyzeResultToken
        {
            public string Token { get; set; }
        }
    }
}
