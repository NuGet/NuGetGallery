// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Search;
using Xunit;

namespace NuGetGallery
{
    public class AutocompleteServicePackageIdsQueryFacts
    {
        private IAppConfiguration GetConfiguration()
        {
            var mockConfiguration = new Mock<IAppConfiguration>();
            mockConfiguration.SetupGet(c => c.ServiceDiscoveryUri).Returns(new Uri("https://api.nuget.org/v3/index.json"));
            mockConfiguration.SetupGet(c => c.AutocompleteServiceResourceType).Returns("SearchAutocompleteService/3.0.0-rc");
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

        private IFeatureFlagService GetFeatureFlagService()
        {
            var mockTelemetryService = new Mock<IFeatureFlagService>();
            return mockTelemetryService.Object;
        }

        [Fact]
        public async Task ExecuteReturns30ResultsForEmptyQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient(), GetFeatureFlagService());
            var result = await query.Execute("", false);
            Assert.True(result.Count() == 30);
        }

        [Fact]
        public async Task ExecuteReturns30ResultsForNullQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient(), GetFeatureFlagService());
            var result = await query.Execute(null, false);
            Assert.True(result.Count() == 30);
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient(), GetFeatureFlagService());
            var result = await query.Execute("jquery", false);
            Assert.Contains("jquery", result, StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(true, null, "?take=30&q=Json&prerelease=True")]
        [InlineData(true, "2.0.0", "?take=30&q=Json&prerelease=True&semVerLevel=2.0.0")]
        [InlineData(false, null, "?take=30&q=Json&prerelease=False")]
        [InlineData(false, "2.0.0", "?take=30&q=Json&prerelease=False&semVerLevel=2.0.0")]
        public void PackageIdQueryBuildsCorrectQueryString(bool includePrerelease, string semVerLevel, string expectedQueryString)
        {
            // Arrange
            var query = new AutocompleteServicePackageIdsQuery(GetConfiguration(), GetResilientSearchClient(), GetFeatureFlagService());

            // Act
            var actualQueryString = query.BuildQueryString("take=30&q=Json", includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedQueryString, actualQueryString);
        }
    }
}
