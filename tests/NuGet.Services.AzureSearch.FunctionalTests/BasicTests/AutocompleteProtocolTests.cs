// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using BasicSearchTests.FunctionalTests.Core.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class AutocompleteProtocolTests : NuGetSearchFunctionalTestBase
    {
        public AutocompleteProtocolTests(CommonFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture, testOutputHelper)
        {
        }

        [Fact]
        public async Task CanGetEmptyResult()
        {
            // Act
            var result = await AutocompleteAsync(new AutocompleteBuilder { Query = Constants.NonExistentSearchString });

            // Assert
            Assert.Equal(0, result.TotalHits);
            Assert.Empty(result.Data);
        }

        [Fact]
        public async Task ShouldGetResultsForEmptyString()
        {
            // Act
            var result = await AutocompleteAsync(new AutocompleteBuilder { Query = "" });

            // Assert
            Assert.True(result.TotalHits.HasValue && result.TotalHits.Value > 0, "No results found, should find at least some results for empty string query.");
            Assert.NotNull(result.Data);
        }

        [Theory]
        [InlineData("DOTnettOoL", Constants.TestPackageId_PackageType)]
        [InlineData("depenDENCY", Constants.TestPackageId)]
        public async Task TreatsPackageTypeAsCaseInsensitive(string packageTypeQuery, string id)
        {
            var searchBuilder = new AutocompleteBuilder
            {
                Query = id,
                PackageType = packageTypeQuery,
            };

            var results = await AutocompleteAsync(searchBuilder);

            Assert.NotEmpty(results.Data);
            Assert.Equal(id, results.Data[0]);
        }

        [Fact]
        public async Task IncludesPackagesWithMatchingPackageType()
        {
            var searchBuilder = new AutocompleteBuilder
            {
                Query = Constants.TestPackageId_PackageType,
                PackageType = "DotNetTool",
            };

            var results = await AutocompleteAsync(searchBuilder);

            Assert.NotEmpty(results.Data);
            Assert.Equal(Constants.TestPackageId_PackageType, results.Data[0]);
        }

        [Fact]
        public async Task PackagesWithoutPackageTypesAreAssumedToBeDependency()
        {
            var searchBuilder = new AutocompleteBuilder
            {
                Query = Constants.TestPackageId,
                PackageType = "Dependency",
            };

            var results = await AutocompleteAsync(searchBuilder);

            Assert.NotEmpty(results.Data);
            Assert.Equal(Constants.TestPackageId, results.Data[0]);
        }

        [Fact]
        public async Task ReturnsNothingWhenThePackageTypeDoesNotExist()
        {
            var searchBuilder = new AutocompleteBuilder
            {
                PackageType = Guid.NewGuid().ToString(),
            };

            var results = await AutocompleteAsync(searchBuilder);

            Assert.Empty(results.Data);
        }

        [Fact]
        public async Task ReturnsNothingWhenThePackageTypeIsInvalid()
        {
            var searchBuilder = new AutocompleteBuilder
            {
                PackageType = "cannot$be:a;package|type",
            };

            var results = await AutocompleteAsync(searchBuilder);

            Assert.Empty(results.Data);
        }
    }
}
