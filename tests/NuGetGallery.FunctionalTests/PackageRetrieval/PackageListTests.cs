// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests.PackageRetrieval
{
    public class PackageListTests : GalleryTestBase
    {
        private readonly Regex TotalDownloadsExpr= new Regex(@"((\d+[,]?)+) total download[s]?");
        private readonly Regex LastUpdatedExpr= new Regex(@"<span data-datetime=""(.+)"">");

        public PackageListTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        private List<long> GetPackagesDownloads(string content)
        {
            return TotalDownloadsExpr
                .Matches(content)
                .Cast<Match>()
                .Select(x => long.Parse(x.Groups[1].Value.Replace(",", ""))).ToList();
        }

        private List<DateTime> GetPackagesUpdateDates(string content)
        {
            var dates = LastUpdatedExpr
                .Matches(content)
                .Cast<Match>()
                .ToList();
                

            return dates.Select(x => DateTime.Parse(x.Groups[1].Value)).ToList();
        }

        private static void AssertIsHtml(HttpResponseMessage response)
        {
            var contentType = Assert.Single(response.Content.Headers.GetValues("Content-Type"));
            Assert.Equal("text/html; charset=utf-8", contentType);
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData("totaldownloads-desc", true)]
        [InlineData("totaldownloads-asc", false)]
        public async Task MakeSureSortedByDownloadsWork(
            string sortBy = "",
            bool expectDescending = true)
        {
            var sortByParam = string.IsNullOrEmpty(sortBy) ? string.Empty : $"&sortBy={sortBy}";
            // Arrange
            var feedUrl = new Uri(
                new Uri(UrlHelper.BaseUrl), 
                $"/packages?q={Constants.TestPackageId}++owner%3A{Constants.TestAccount}{sortByParam}");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                var downloads = GetPackagesDownloads(content);
                var expectedDownloads = expectDescending ? downloads.OrderByDescending(x => x) : downloads.Select(x => x);
                Assert.Contains($"/packages/{Constants.TestPackageId}", content); // It found the package
                Assert.Equal(expectedDownloads, downloads); // The downloads are sorted as expected
            }
        }

        [Theory]
        [InlineData("created-desc")]
        [InlineData("cReAtEd-DeSc")]
        public async Task MakeSureLastUpodatedSortingWorks(string sortBy)
        {
            var sortByParam = string.IsNullOrEmpty(sortBy) ? string.Empty : $"&sortBy={sortBy}";
            // Arrange
            var feedUrl = new Uri(
                new Uri(UrlHelper.BaseUrl),
                $"/packages?q={Constants.TestPackageId}++owner%3A{Constants.TestAccount}{sortByParam}");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                var updateDates = GetPackagesUpdateDates(content);
                var expectedDates = updateDates.OrderByDescending(x => x);
                Assert.Equal(expectedDates, updateDates); // The "recently updated" dates are sorted as expected
            }
        }

        [Theory]
        [Priority(2)]
        [Category("P2Tests")]
        [InlineData(Constants.TestPackageId, "dependency")]
        [InlineData(Constants.TestPackageIdDotNetTool, "dotnettool")]
        [InlineData(Constants.TestPackageIdARandomType, "ARandomType")]
        [InlineData(Constants.TestPackageIdTemplate, "template")]
        public async Task ExpectPackageTypeFilterToWork(
            string id,
            string packageType = "")
        {
            var packageTypeParam = string.IsNullOrEmpty(packageType) ? string.Empty : $"&packageType={packageType}";
            // Arrange
            var feedUrl = new Uri(
                new Uri(UrlHelper.BaseUrl),
                $"/packages?q={id}++owner%3A{Constants.TestAccount}{packageTypeParam}");

            // Act
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(feedUrl))
            {
                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertIsHtml(response);

                var content = await response.Content.ReadAsStringAsync();
                Assert.Contains($"/packages/{id}", content);
            }
        }
    }
}
