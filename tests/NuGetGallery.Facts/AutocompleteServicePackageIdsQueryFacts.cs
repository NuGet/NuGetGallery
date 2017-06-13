﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Configuration;
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

        [Fact]
        public async Task ExecuteReturns30ResultsForEmptyQuery()
        {
            var query = new AutoCompleteServicePackageIdsQuery(GetConfiguration());
            var result = await query.Execute("", false);
            Assert.True(result.Count() == 30);
        }

        [Fact]
        public async Task ExecuteReturns30ResultsForNullQuery()
        {
            var query = new AutoCompleteServicePackageIdsQuery(GetConfiguration());
            var result = await query.Execute(null, false);
            Assert.True(result.Count() == 30);
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutoCompleteServicePackageIdsQuery(GetConfiguration());
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
            var query = new AutoCompleteServicePackageIdsQuery(GetConfiguration());

            // Act
            var actualQueryString = query.BuildQueryString("take=30&q=Json", includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedQueryString, actualQueryString);
        }
    }
}
