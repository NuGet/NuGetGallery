// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NgTests.Data;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace NgTests
{
    public class Feed2CatalogTests
    {
        [Fact]
        public async Task CreatesNewCatalogFromCreatedAndEditedPackages()
        {
            // Arrange
            var catalogStorage = new MemoryStorage();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.Add(
                new Uri(auditingStorage.BaseAddress, "2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));
            
            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);

            mockServer.SetAction("/package/ListedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(mockServer, "http://tempuri.org", catalogStorage, auditingStorage, null, TimeSpan.FromMinutes(5), 20, true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(5, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"0001-01-01", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            // Ensure catalog does not have the deleted "OtherPackage" as a fresh catalog should not care about deletes
            var package4 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json"));
            Assert.Null(package4.Key);
        }

        [Fact]
        public async Task AppendsDeleteToExistingCatalog()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.Add(
                new Uri(auditingStorage.BaseAddress, "2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);

            mockServer.SetAction("/package/ListedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(mockServer, "http://tempuri.org", catalogStorage, auditingStorage, null, TimeSpan.FromMinutes(5), 20, true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(6, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T00:00:00Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"2015-01-01T01:01:01", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/otherpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"OtherPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            // Ensure catalog has the delete of "OtherPackage"
            var package4 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json"));
            Assert.NotNull(package4.Key);
            Assert.Contains("\"PackageDelete\",", package4.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package4.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package4.Value.GetContentString());
        }

        [Fact]
        public async Task AppendsDeleteAndReinsertToExistingCatalog()
        {
            // Arrange
            var catalogStorage = Catalogs.CreateTestCatalogWithThreePackages();
            var auditingStorage = new MemoryStorage();
            auditingStorage.Content.Add(
                new Uri(auditingStorage.BaseAddress, "2015-01-01T00:01:01-deleted.audit.v1.json"),
                new StringStorageContent(TestCatalogEntries.DeleteAuditRecordForOtherPackage100));

            var mockServer = new MockServerHttpClientHandler();

            mockServer.SetAction(" / ", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetCreatedPackages);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetCreatedPackagesSecondRequest);
            mockServer.SetAction("/Packages?$filter=Created%20gt%20DateTime'2015-01-01T01:01:03.0000000Z'&$top=20&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);

            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'0001-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEditedPackages);
            mockServer.SetAction("/Packages?$filter=LastEdited%20gt%20DateTime'2015-01-01T00:00:00.0000000Z'&$top=20&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl", GetEmptyPackages);
            
            mockServer.SetAction("/package/ListedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.0.zip")) }));
            mockServer.SetAction("/package/ListedPackage/1.0.1", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\ListedPackage.1.0.1.zip")) }));
            mockServer.SetAction("/package/UnlistedPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\UnlistedPackage.1.0.0.zip")) }));
            mockServer.SetAction("/package/OtherPackage/1.0.0", request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead("Packages\\OtherPackage.1.0.0.zip")) }));

            // Act
            var feed2catalogTestJob = new TestableFeed2CatalogJob(mockServer, "http://tempuri.org", catalogStorage, auditingStorage, null, TimeSpan.FromMinutes(5), 20, true);
            await feed2catalogTestJob.RunOnce(CancellationToken.None);

            // Assert
            Assert.Equal(7, catalogStorage.Content.Count);

            // Ensure catalog has index.json
            var catalogIndex = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("index.json"));
            Assert.NotNull(catalogIndex.Key);
            Assert.Contains("\"nuget:lastCreated\":\"2015-01-01T01:01:03Z\"", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastDeleted\":\"2015-01-01T01:01:01", catalogIndex.Value.GetContentString());
            Assert.Contains("\"nuget:lastEdited\":\"2015-01-01T00:00:00", catalogIndex.Value.GetContentString());

            // Ensure catalog has page0.json
            var pageZero = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("page0.json"));
            Assert.NotNull(pageZero.Key);
            Assert.Contains("\"parent\":\"http://tempuri.org/index.json\",", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/listedpackage.1.0.1.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"ListedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.1\"", pageZero.Value.GetContentString());

            Assert.Contains("/unlistedpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"UnlistedPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            Assert.Contains("/otherpackage.1.0.0.json\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:id\":\"OtherPackage\",", pageZero.Value.GetContentString());
            Assert.Contains("\"nuget:version\":\"1.0.0\"", pageZero.Value.GetContentString());

            // Check individual package entries
            var package1 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.0.json"));
            Assert.NotNull(package1.Key);
            Assert.Contains("\"PackageDetails\",", package1.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package1.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package1.Value.GetContentString());

            var package2 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/listedpackage.1.0.1.json"));
            Assert.NotNull(package2.Key);
            Assert.Contains("\"PackageDetails\",", package2.Value.GetContentString());
            Assert.Contains("\"id\": \"ListedPackage\",", package2.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.1\",", package2.Value.GetContentString());

            var package3 = catalogStorage.Content.FirstOrDefault(pair => pair.Key.PathAndQuery.EndsWith("/unlistedpackage.1.0.0.json"));
            Assert.NotNull(package3.Key);
            Assert.Contains("\"PackageDetails\",", package3.Value.GetContentString());
            Assert.Contains("\"id\": \"UnlistedPackage\",", package3.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package3.Value.GetContentString());

            // Ensure catalog has the delete of "OtherPackage"
            var package4 = catalogStorage.Content.FirstOrDefault(pair =>
                pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json")
                && pair.Value.GetContentString().Contains("\"PackageDelete\""));
            Assert.NotNull(package4.Key);
            Assert.Contains("\"PackageDelete\",", package4.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package4.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package4.Value.GetContentString());

            // Ensure catalog has the insert of "OtherPackage"
            var package5 = catalogStorage.Content.FirstOrDefault(pair =>
                pair.Key.PathAndQuery.EndsWith("/otherpackage.1.0.0.json")
                && pair.Value.GetContentString().Contains("\"PackageDetails\""));
            Assert.NotNull(package5.Key);
            Assert.Contains("\"PackageDetails\",", package5.Value.GetContentString());
            Assert.Contains("\"id\": \"OtherPackage\",", package5.Value.GetContentString());
            Assert.Contains("\"version\": \"1.0.0\",", package5.Value.GetContentString());
        }

        private Task<HttpResponseMessage> GetCreatedPackages(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "ListedPackage",
                    Version = "1.0.0",
                    Description = "Listed package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2015, 1, 1),
                    LastEdited = new DateTime(2015, 1, 1),
                    Published = new DateTime(2015, 1, 1)
                },
                new ODataPackage
                {
                    Id = "UnlistedPackage",
                    Version = "1.0.0",
                    Description = "Unlisted package",
                    Hash = "",
                    Listed = false,

                    Created = new DateTime(2015, 1, 1),
                    LastEdited = new DateTime(2015, 1, 1),
                    Published = Convert.ToDateTime("1900-01-01T00:00:00Z")
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri("http://tempuri.org"), "Packages"))
            });
        }

        private Task<HttpResponseMessage> GetEditedPackages(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "ListedPackage",
                    Version = "1.0.1",
                    Description = "Listed package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2015, 1, 1),
                    LastEdited = new DateTime(2015, 1, 1),
                    Published = new DateTime(2015, 1, 1)
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri("http://tempuri.org"), "Packages"))
            });
        }

        private Task<HttpResponseMessage> GetEmptyPackages(HttpRequestMessage request)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(Enumerable.Empty<ODataPackage>(), new Uri("http://tempuri.org"), "Packages"))
            });
        }

        private Task<HttpResponseMessage> GetCreatedPackagesSecondRequest(HttpRequestMessage request)
        {
            var packages = new List<ODataPackage>
            {
                new ODataPackage
                {
                    Id = "OtherPackage",
                    Version = "1.0.0",
                    Description = "Other package",
                    Hash = "",
                    Listed = true,

                    Created = new DateTime(2015, 1, 1, 1, 1, 3),
                    LastEdited = new DateTime(2015, 1, 1, 1, 1, 3),
                    Published = new DateTime(2015, 1, 1, 1, 1, 3)
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    ODataFeedHelper.ToODataFeed(packages, new Uri("http://tempuri.org"), "Packages"))
            });
        }
    }
}
