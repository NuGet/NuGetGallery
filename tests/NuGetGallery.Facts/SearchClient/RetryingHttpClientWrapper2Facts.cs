// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class RetryingHttpClientWrapper2Facts
    {
        private static readonly Uri ValidUri1 = new Uri("http://www.microsoft.com");
        private static readonly Uri InvalidUri1 = new Uri("http://nonexisting.domain.atleast.ihope");
        private static readonly Uri InvalidUriWith404 = new Uri("http://www.nuget.org/thisshouldreturna404page");

        private RetryingHttpClientWrapper2 CreateWrapperClient()
        {
            return new RetryingHttpClientWrapper2(credentials: null, onException: (exception) => { });
        }

        [Fact]
        public async Task ReturnsStringForValidUri()
        {
            // Arrange
            var client = CreateWrapperClient();

            // Act
            var result = await client.GetStringAsync(new[] { ValidUri1 });

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ReturnsSuccessResponseForValidUri()
        {
            // Arrange
            var client = CreateWrapperClient();

            // Act
            var result = await client.GetAsync(new[] { ValidUri1 });

            // Assert
            Assert.True(result.IsSuccessStatusCode);
        }

        [Fact]
        public async Task ThrowsForInvalidUri()
        {
            // Arrange
            var client = CreateWrapperClient();

            // Act + Assert
            await Assert.ThrowsAsync<HttpRequestException>( async () => await client.GetStringAsync(new[] { InvalidUri1 }));
        }


        [Fact]
        public async Task Returns404When404IsExpected()
        {
            // Arrange
            var client = CreateWrapperClient();

            // Act
            var result = await client.GetAsync(new[] { InvalidUriWith404 });

            // Assert
            Assert.False(result.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }
    }
}
