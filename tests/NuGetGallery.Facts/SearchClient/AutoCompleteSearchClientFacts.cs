// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class AutoCompleteSearchClientFacts
    {
        private static readonly Uri BaseAddress = new Uri("https://api-v2v3search-0.nuget.org");
        private static readonly Uri ValidUri2 = new Uri("https://api-v2v3search-0.nuget.org/?id=System.Collections");
        private static readonly Uri InvalidUri1 = new Uri("http://nonexisting.domain.atleast.ihope");
        private static readonly Uri InvalidUriWith404 = new Uri("http://www.nuget.org/thisshouldreturna404page");

        private AutoCompleteSearchClient CreateClient()
        {
            var client = new HttpClient();
            client.BaseAddress = BaseAddress;
            return new AutoCompleteSearchClient(client);
        }

        [Fact]
        public async Task ReturnsStringForValidUri()
        {
            // Arrange
            string queryString = "id=System.Collections";
            var client = CreateClient();

            // Act
            var result = await client.GetStringAsync(queryString);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void ReturnsCorrectPath()
        {
            // Arrange
            string queryString = "id=System.Collections";
            var client = CreateClient();

            // Act
            var result = client.AppendAutocompleteUriPath(queryString);

            // Assert
            Assert.Equal("https://api-v2v3search-0.nuget.org/autocomplete?id=System.Collections", result.AbsoluteUri);
        }
    }
}
