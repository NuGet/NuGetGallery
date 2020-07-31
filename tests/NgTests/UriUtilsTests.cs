// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;
using System;
using Xunit;
using NuGet.Versioning;

namespace NgTests
{
    public class UriUtilsTests
    {
        [Theory]
        // Packages
        [InlineData("https://api.nuget.org/packages/newtonsoft.json.9.0.1.nupkg")]
        [InlineData("https://api.nuget.org/packages/findpackagesbyid.1.0.0-findpackagesbyid.nupkg")]
        [InlineData("https://api.nuget.org/packages/search.1.0.0-search.nupkg")]
        [InlineData("https://api.nuget.org/packages/packages.1.0.0-packages.nupkg")]
        // Index
        [InlineData("https://api.nuget.org/v3/index.json")]
        // Catalog
        [InlineData("https://api.nuget.org/v3/catalog0/index.json")]
        [InlineData("https://api.nuget.org/v3/catalog0/page0.json")]
        [InlineData("https://api.nuget.org/v3/catalog0/data/2015.02.01.06.22.45/adam.jsgenerator.1.1.0.json")]
        // Registration
        [InlineData("https://api.nuget.org/v3/registration0/newtonsoft.json/index.json")]
        [InlineData("https://api.nuget.org/v3/registration0/newtonsoft.json/9.0.1.json")]
        [InlineData("https://api.nuget.org/v3/registration0/findpackagesbyid/1.0.0-findpackagesbyid.json")]
        [InlineData("https://api.nuget.org/v3/registration0/search/1.0.0-search.json")]
        [InlineData("https://api.nuget.org/v3/registration0/packages/1.0.0-packages.json")]
        // Flat-Container
        [InlineData("https://api.nuget.org/v3/flatcontainer/newtonsoft.json/index.json")]
        [InlineData("https://api.nuget.org/v3/flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg")]
        [InlineData("https://api.nuget.org/v3/flatcontainer/findpackagesbyid/1.0.0-findpackagesbyid/findpackagesbyid.1.0.0-findpackagesbyid.nupkg")]
        [InlineData("https://api.nuget.org/v3/flatcontainer/search/1.0.0-search/search.1.0.0-search.nupkg")]
        [InlineData("https://api.nuget.org/v3/flatcontainer/packages/1.0.0-packages/packages.1.0.0-packages.nupkg")]
        // Search service
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/search/query?q=id:'Newtonsoft.Json'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/query?q=id:'Newtonsoft.Json'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/search/query?q=id:'FindPackagesById'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/query?q=id:'FindPackagesById'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/search/query?q=id:'Search'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/query?q=id:'Search'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/search/query?q=id:'Packages'")]
        [InlineData("http://nuget-prod-0-v2v3search.cloudapp.net/query?q=id:'Packages'")]
        public void GetNonhijackableUri_ReturnsOriginalUri(string originalUriString)
        {
            // Arrange
            var originalUri = new Uri(originalUriString);

            // Act
            var newUri = UriUtils.GetNonhijackableUri(originalUri);

            // Assert
            Assert.Equal(originalUriString, newUri.ToString());
            Assert.Same(originalUri, newUri);
        }

        [Theory]
        // Already has orderby
        [InlineData("https://www.nuget.org/api/v2/Packages?$orderby=Id", "https://www.nuget.org/api/v2/Packages?$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/Packages?$orderby=Id&$filter=IsLatestVersion", "https://www.nuget.org/api/v2/Packages?$orderby=Version&$filter=IsLatestVersion")]
        [InlineData("https://www.nuget.org/api/v2/Search()?$filter=IsAbsoluteLatestVersion&$skip=0&$top=30&searchTerm='pickles'&targetFramework='net45'&includePrerelease=true&$orderby=Id", "https://www.nuget.org/api/v2/Search()?$filter=IsAbsoluteLatestVersion&$skip=0&$top=30&searchTerm='pickles'&targetFramework='net45'&includePrerelease=true&$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/Search()?$filter=IsAbsoluteLatestVersion&$skip=0&$orderby=Id&$top=30&searchTerm='pickles'&targetFramework='net45'&includePrerelease=true", "https://www.nuget.org/api/v2/Search()?$filter=IsAbsoluteLatestVersion&$skip=0&$orderby=Version&$top=30&searchTerm='pickles'&targetFramework='net45'&includePrerelease=true")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?$filter=IsLatestVersion&$top=1&id='MySql.Data'&$orderby=Id", "https://www.nuget.org/api/v2/FindPackagesById()?$filter=IsLatestVersion&$top=1&id='MySql.Data'&$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?$filter=IsLatestVersion&$orderby=Id&$top=1&id='MySql.Data'", "https://www.nuget.org/api/v2/FindPackagesById()?$filter=IsLatestVersion&$orderby=Version&$top=1&id='MySql.Data'")]
        // Packages(Id='...',Version='...')
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='Microsoft.Owin.Security.Facebook',Version='3.0.1')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'Microsoft.Owin.Security.Facebook' and NormalizedVersion eq '3.0.1'&semVerLevel=2.0.0")]
        // Packages endpoint without orderby
        [InlineData("https://www.nuget.org/api/v2/Packages", "https://www.nuget.org/api/v2/Packages?$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/Packages?$top=10", "https://www.nuget.org/api/v2/Packages?$top=10&$orderby=Version")]
        // Search endpoint without orderby
        [InlineData("https://www.nuget.org/api/v2/Search()", "https://www.nuget.org/api/v2/Search()?$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/Search()?$top=10", "https://www.nuget.org/api/v2/Search()?$top=10&$orderby=Version")]
        // FindPackagesById endpoint without orderby
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()", "https://www.nuget.org/api/v2/FindPackagesById()?$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?id='Microsoft.Rest.ClientRuntime'", "https://www.nuget.org/api/v2/FindPackagesById()?id='Microsoft.Rest.ClientRuntime'&$orderby=Version")]
        // Id and version contain names of other endpoints
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='FindPackagesById',Version='1.0.0')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'FindPackagesById' and NormalizedVersion eq '1.0.0'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='Search',Version='1.0.0')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'Search' and NormalizedVersion eq '1.0.0'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='Packages',Version='1.0.0')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'Packages' and NormalizedVersion eq '1.0.0'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='abcd',Version='1.0.0-FindPackagesById')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'abcd' and NormalizedVersion eq '1.0.0-FindPackagesById'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='abcd',Version='1.0.0-Search')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'abcd' and NormalizedVersion eq '1.0.0-Search'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/Packages(Id='abcd',Version='1.0.0-Packages')", "https://www.nuget.org/api/v2/Packages?$filter=true and Id eq 'abcd' and NormalizedVersion eq '1.0.0-Packages'&semVerLevel=2.0.0")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?id='Search'", "https://www.nuget.org/api/v2/FindPackagesById()?id='Search'&$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?id='FindPackagesById'", "https://www.nuget.org/api/v2/FindPackagesById()?id='FindPackagesById'&$orderby=Version")]
        [InlineData("https://www.nuget.org/api/v2/FindPackagesById()?id='Packages'", "https://www.nuget.org/api/v2/FindPackagesById()?id='Packages'&$orderby=Version")]
        public void GetNonhijackableUri_ReturnsNonhijackableUri(string originalUriString, string expectedUriString)
        {
            // Arrange
            var originalUri = new Uri(originalUriString);

            // Act
            var newUri = UriUtils.GetNonhijackableUri(originalUri);

            // Assert
            Assert.NotEqual(originalUriString, newUri.ToString());
            Assert.NotSame(originalUri, newUri);

            Assert.Equal(expectedUriString, newUri.ToString());
        }

        [Theory]
        [InlineData("1.00")]
        [InlineData("1.01.1")]
        [InlineData("1.00.0.1")]
        [InlineData("1.0.0.0")]
        [InlineData("1.0.01.0")]
        public void GetNonhijackableUri_NormalizesPackagesVersion(string version)
        {
            // Arrange
            var id = "abcd";
            var originalUri = new Uri($"https://www.nuget.org/api/v2/Packages(Id='{id}',Version='{version}')");
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();

            // Act
            var newUri = UriUtils.GetNonhijackableUri(originalUri);

            // Assert
            Assert.NotEqual(originalUri.ToString(), newUri.ToString());
            Assert.NotSame(originalUri, newUri);

            Assert.Equal($"https://www.nuget.org/api/v2/Packages?$filter=true and Id eq '{id}' and NormalizedVersion eq '{normalizedVersion}'&semVerLevel=2.0.0", newUri.ToString());
        }
    }
}