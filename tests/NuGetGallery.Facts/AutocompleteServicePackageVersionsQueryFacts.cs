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
            mockConfiguration.SetupGet(c => c.AutocompleteServiceResourceType).Returns("SearchAutocompleteService/3.0.0-rc");
            return mockConfiguration.Object;
        }

        [Fact]
        public async Task ExecuteThrowsForEmptyId()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration());
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await query.Execute("", false));
        }

        [Fact]
        public async Task ExecuteReturnsResultsForSpecificQuery()
        {
            var query = new AutocompleteServicePackageVersionsQuery(GetConfiguration());
            var result = await query.Execute("newtonsoft.json", false);
            Assert.NotEmpty(result);
            Assert.True(result.Any());
        }
    }
}