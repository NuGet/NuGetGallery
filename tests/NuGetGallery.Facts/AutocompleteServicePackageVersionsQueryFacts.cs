// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class AutocompleteServicePackageVersionsQueryFacts
    {
        private IAppConfiguration GetConfiguration()
        {
            var mockConfiguration = new Mock<IAppConfiguration>();
            mockConfiguration.SetupGet(c => c.ServiceDiscoveryUri).Returns(new Uri("https://api.nuget.org/v3/index.json"));
            mockConfiguration.SetupGet(c => c.AutocompleteServiceResourceType).Returns("SearchAutocompleteService");
            return mockConfiguration.Object;
        }

        [Fact]
        public async Task ExecuteThrowsForEmptyId()
        {
            var query = new AutoCompleteServicePackageVersionsQuery(GetConfiguration());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await query.Execute(string.Empty, false));
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutoCompleteServicePackageVersionsQuery(GetConfiguration());
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
            var query = new AutoCompleteServicePackageVersionsQuery(GetConfiguration());

            // Act
            var actualQueryString = query.BuildQueryString("id=Newtonsoft.Json", includePrerelease, semVerLevel);

            // Assert
            Assert.Equal(expectedQueryString, actualQueryString);
        }
    }
}