// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
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
            var client = new HttpClient();
            client.BaseAddress = searchUri;
            return new GallerySearchClient(client);
        }

        [Fact]
        public void GetTheSearchEndPoint()
        {
            // Arrange
            var client = CreateClient(new Uri(_uri));

            // Act
            var result = client.GetSearchUri("q=newtonsoft"); ;

            // Assert
            Assert.Equal(_uri + "search/query?q=newtonsoft", result.AbsoluteUri);
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
            Assert.Throws<ArgumentNullException>(() => new GallerySearchClient(null));
        }
    }
}
