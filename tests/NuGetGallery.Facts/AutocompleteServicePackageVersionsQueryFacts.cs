// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Search;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class AutocompleteServicePackageVersionsQueryFacts
    {
        private IAppConfiguration GetConfiguration()
        {
            var mockConfiguration = new Mock<IAppConfiguration>();
            return mockConfiguration.Object;
        }

        private ILogger<ResilientSearchHttpClient> GetLogger()
        {
            var mockConfiguration = new Mock<ILogger<ResilientSearchHttpClient>>();
            return mockConfiguration.Object;
        }

        private IResilientSearchClient GetResilientSearchClient()
        {
            var mockTelemetryService = new Mock<ITelemetryService>();
            List<IHttpClientWrapper> clients = new List<IHttpClientWrapper>();
            clients.Add(new HttpClientWrapper(new HttpClient()
            {
                BaseAddress = new Uri("https://api-v2v3search-0.nuget.org")
            }));
            return new ResilientSearchHttpClient(clients, GetLogger(), mockTelemetryService.Object);
        }

        [Fact]
        public async Task ExecuteThrowsForEmptyId()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await query.Execute(string.Empty, false));
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());
            var result = await query.Execute("newtonsoft.json", false);
            Assert.True(result.Any());
        }

        [Theory]
        [InlineData(true, null, "?id=Newtonsoft.Json&prerelease=True")]
        [InlineData(true, "2.0.0", "?id=Newtonsoft.Json&prerelease=True&semVerLevel=2.0.0")]
        [InlineData(false, null, "?id=Newtonsoft.Json&prerelease=False")]
        [InlineData(false, "2.0.0", "?id=Newtonsoft.Json&prerelease=False&semVerLevel=2.0.0")]
        public void PackageVersionsQueryBuildsCorrectQueryString(bool includePrerelease, string semVerLevel, string expectedQueryString)
        {
            // Arrange
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration(), GetResilientSearchClient());

            // Act
            var actualQueryString = query.BuildQueryString("id=Newtonsoft.Json", includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedQueryString, actualQueryString);
        }
    }
}