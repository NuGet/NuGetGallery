// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core.Models;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using System.Net;

namespace BasicSearchTests.FunctionalTests.Core
{
    public class AutocompleteFunctionalTests : BaseFunctionalTests
    {
        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var response = await Client.GetAsync(new AutocompleteBuilder { Query = Constants.NonExistentSearchString }.RequestUri);
            var result = await response.Content.ReadAsAsync<AutocompleteResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var response = await Client.GetAsync(new AutocompleteBuilder().RequestUri);
            var result = await response.Content.ReadAsAsync<AutocompleteResult>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "Found 0 hits for empty string autocomplete");
            Assert.NotNull(result.Data);
        }
    }
}