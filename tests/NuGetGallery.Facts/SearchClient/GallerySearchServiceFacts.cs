// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Xunit;

namespace NuGetGallery.SearchClient
{
    public class GallerySearchServiceFacts
    {
        private static string _uri = "https://api-v2v3search-0.nuget.org/";
        private static string _diagUri = "https://api-v2v3search-0.nuget.org/search/diag";
        private GallerySearchClient CreateClient(Uri searchUri)
        {
            return new GallerySearchClient(searchUri, null, (ex) => { });
        }

        [Fact]
        public async Task GetTheSearchEndPoint()
        {
            // Arrange
            var client = CreateClient(new Uri(_uri));

            // Act
            var result = await client.GetEndpoints();

            // Assert
            Assert.Equal(1, result.Count());
            Assert.Equal(_uri, result.ElementAt(0).AbsoluteUri);
        }


        [Fact]
        public void GetTheDiagnostics()
        {
            // Arrange
            var client = CreateClient(new Uri(_uri));

            // Act
            var result = client.GetDiagnosticsUri();

            // Assert
            Assert.Equal(_diagUri, result.AbsoluteUri);
        }

        [Fact]
        public void CtorNullArgException()
        {
            // Arrange + Act + Assert
            Assert.Throws<ArgumentNullException>(() => CreateClient(null));
        }
    }
}
