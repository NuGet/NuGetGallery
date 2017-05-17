﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionalTests;

namespace NuGetGallery.LoadTests
{
    /// <summary>
    /// This class has the various scenarios used in LoadTests.
    /// The tests does minimal validation and uses existing packages to reduce the execution time spent in test prep and asserts.
    /// </summary>
    [TestClass]
    public class LoadTests
    {
        public LoadTests()
        {
            // suppress SSL validation for *.cloudapp.net
            ServicePointManagerInitializer.InitializeServerCertificateValidationCallback();
        }

        [TestMethod]
        [Description("Tries to download a packages from v2 feed and make sure the re-direction happens properly.")]
        [TestCategory("P0Tests")]
        public async Task DownloadPackageSimulationTest()
        {
            string packageId = "EntityFramework"; //try to down load a pre-defined package.
            string version = "5.0.0";
            //Just try download and not actual download. Since this will be used in load test, we don't to actually download the nupkg everytime.
            var odataHelper = new ODataHelper();
            string redirectUrl = await odataHelper.TryDownloadPackageFromFeed(packageId, version);
            Assert.IsNotNull(redirectUrl, " Package download from V2 feed didnt work");
            string expectedSubString = "packages/entityframework.5.0.0.nupkg";
            Assert.IsTrue(redirectUrl.Contains(expectedSubString), " The re-direct Url {0} doesnt contain the expect substring {1}", redirectUrl, expectedSubString);
        }

        [TestMethod]
        [Description("Tries to simulate the launch of Manage package dialog UI")]
        [TestCategory("P0Tests")]
        public async Task ManagePackageUILaunchSimulationTest()
        {
            // api/v2/search()/$count query is made everytime Manage package UI is launched in VS.
            //This test simulates the same.
            var request = WebRequest.Create(UrlHelper.V2FeedRootUrl + @"/Search()/$count?$filter=IsLatestVersion&searchTerm=''&targetFramework='net45'&includePrerelease=false");
            var response = await request.GetResponseAsync();
            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }
            int searchCount = Convert.ToInt32(responseText);

            //Just check if the response is a valid int.
            Assert.IsTrue(searchCount >= 0);
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with predefined packageId")]
        [TestCategory("P0Tests")]
        public async Task FindPackagesByIdForPredefinedPackage()
        {
            string packageId = "PostSharp";
            string url = UrlHelper.V2FeedRootUrl + @"/FindPackagesById()?id='" + packageId + "'";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            var odataHelper = new ODataHelper();
            var containsResponseText = await odataHelper.ContainsResponseText(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for FindPackagesById with specific packageId and version")]
        [TestCategory("P0Tests")]
        public async Task FindPackagesBySpecificIdAndVersion()
        {
            string packageId = "Microsoft.Web.Infrastructure";
            string version = "1.0.0";
            string url = UrlHelper.V2FeedRootUrl + @"Packages(Id='" + packageId + "',Version='" + version + "')";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId + "',Version='" + version + "')</id>";
            var odataHelper = new ODataHelper();
            var containsResponseText = await odataHelper.ContainsResponseText(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [TestCategory("P0Tests")]
        public async Task PackagesApiTest()
        {
            string packageId = "newtonsoft.json";
            string url = UrlHelper.V2FeedRootUrl + @"Packages()?$filter=tolower(Id) eq '" + packageId + "'&$orderby=Id";
            string expectedText = @"<id>" + UrlHelper.V2FeedRootUrl + "Packages(Id='" + packageId;
            var odataHelper = new ODataHelper();
            var containsResponseText = await odataHelper.ContainsResponseTextIgnoreCase(url, expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse for PackagesApi test with specific packageId ")]
        [TestCategory("P1Tests")]
        public async Task StatsTotalTest()
        {
            string url = UrlHelper.BaseUrl + @"/stats/totals";
            var odataHelper = new ODataHelper();
            var containsResponseText = await odataHelper.ContainsResponseText(url, @"Downloads");
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Hits the search endpoint directly")]
        [TestCategory("P0Tests")]
        public async Task HitSearchEndPointDirectly()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;

            string requestUri = UrlHelper.SearchServiceBaseUrl + "search/query?q='app insights'&luceneQuery=false";
            HttpResponseMessage response;

            using (var client = new HttpClient(handler))
            {
                response = await client.GetAsync(requestUri);
            }

            Console.WriteLine("HTTP status code : {0}", response.StatusCode);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [Description("Verify the webresponse from top30 packages feed contains jQuery")]
        [TestCategory("P0Tests")]
        public async Task Top30PackagesFeedTest()
        {
            string url = UrlHelper.V2FeedRootUrl + @"/Search()?$filter=IsAbsoluteLatestVersion&$orderby=DownloadCount%20desc,Id&$skip=0&$top=30&searchTerm=''&targetFramework='net45'&includePrerelease=true";
            var odataHelper = new ODataHelper();
            bool containsResponseText = await odataHelper.ContainsResponseText(url, "jQuery");
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/$metadata contains the V2FeedPackage text")]
        [TestCategory("P0Tests")]
        public async Task ApiV2MetadataTest()
        {
            string expectedText = @"V2FeedPackage";
            var odataHelper = new ODataHelper();
            bool containsResponseText = await odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl + @"$metadata", expectedText);
            Assert.IsTrue(containsResponseText);
        }

        [TestMethod]
        [Description("Verify the webresponse from /Api/V2/ feed contains the Packages text")]
        [TestCategory("P0Tests")]
        public async Task ApiV2BaseUrlTest()
        {
            string expectedText1 = @"<atom:title>Packages</atom:title>";
            string expectedText2 = @"<atom:title type=""text"">Packages</atom:title>";
            var odataHelper = new ODataHelper();
            bool containsResponseText1 = await odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText1);
            bool containsResponseText2 = await odataHelper.ContainsResponseText(UrlHelper.V2FeedRootUrl, expectedText2);
            Assert.IsTrue(containsResponseText1 || containsResponseText2);
        }

        [TestMethod]
        [Description("Performs a querystring-based search of the Microsoft curated feed. Confirms expected packages are returned.")]
        [TestCategory("P0Tests")]
        public async Task SearchMicrosoftDotNetCuratedFeed()
        {
            var packageId = "microsoft.aspnet.webpages";
            var request = WebRequest.Create(UrlHelper.DotnetCuratedFeedUrl + @"Packages()?$filter=tolower(Id)%20eq%20'" + packageId + "'&$orderby=Id&$skip=0&$top=30");
            var response = await request.GetResponseAsync();

            string responseText;
            using (var sr = new StreamReader(response.GetResponseStream()))
            {
                responseText = await sr.ReadToEndAsync();
            }

            string packageUrl = @"<id>" + UrlHelper.DotnetCuratedFeedUrl + "Packages(Id='" + packageId;
            Assert.IsTrue(responseText.ToLowerInvariant().Contains(packageUrl.ToLowerInvariant()));
        }
    }
}
