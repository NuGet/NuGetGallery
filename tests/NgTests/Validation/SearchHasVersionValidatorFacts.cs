// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class SearchHasVersionValidatorFacts
    {
        public class ValidateAsync : FactsBase
        {
            [Fact]
            public async Task FailsIfPackageIsListedInDatabaseButUnlistedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = true };
                MakeEmptyResultInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Fail, result.Result);
                var exception = Assert.IsType<MetadataInconsistencyException>(result.Exception);
                Assert.Equal(
                    "The metadata between the database and V3 is inconsistent! Database shows listed but search shows unlisted.",
                    exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageIsListedInDatabaseButOtherVersionIsListedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = true };
                MakeVersionVisibleInSearch(OtherVersion);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Fail, result.Result);
                var exception = Assert.IsType<MetadataInconsistencyException>(result.Exception);
                Assert.Equal(
                    "The metadata between the database and V3 is inconsistent! Database shows listed but search shows unlisted.",
                    exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageIsUnavailableInDatabaseButListedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = null;
                MakeVersionVisibleInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Fail, result.Result);
                var exception = Assert.IsType<MetadataInconsistencyException>(result.Exception);
                Assert.Equal(
                    "The metadata between the database and V3 is inconsistent! Database shows unavailable but search shows listed.",
                    exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageIsUnlistedInDatabaseButListedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = false };
                MakeVersionVisibleInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Fail, result.Result);
                var exception = Assert.IsType<MetadataInconsistencyException>(result.Exception);
                Assert.Equal(
                    "The metadata between the database and V3 is inconsistent! Database shows unlisted but search shows listed.",
                    exception.Message);
            }

            [Fact]
            public async Task SucceedsIfPackageIsUnavailableInDatabaseAndOtherVersionInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = null;
                MakeVersionVisibleInSearch(OtherVersion);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
            }

            [Fact]
            public async Task SucceedsIfPackageIsUnavailableInDatabaseAndUnlistedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = null;
                MakeEmptyResultInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
            }

            [Fact]
            public async Task SucceedsIfPackageIsUnlistedInDatabaseAndOtherVersionInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = false };
                MakeEmptyResultInSearch(OtherVersion);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
            }

            [Fact]
            public async Task SucceedsIfPackageIsUnlistedInDatabaseAndUnlistedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = false };
                MakeEmptyResultInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
            }

            [Fact]
            public async Task SucceedsIfPackageIsListedInDatabaseAndListedInSearch()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext();
                DatabaseIndex = new PackageRegistrationIndexMetadata { Listed = true };
                MakeVersionVisibleInSearch();

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
            }
        }

        public class FactsBase
        {
            public const string OtherVersion = "9.9.9";
            public static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            public static readonly DateTime PackageCreationTime = DateTime.UtcNow;

            public CatalogIndexEntry[] CatalogEntries { get; }
            public MockServerHttpClientHandler MockServer { get; }
            public PackageRegistrationIndexMetadata DatabaseIndex { get; set; }

            public FactsBase()
            {
                MockServer = new MockServerHttpClientHandler();

                // Mock a catalog entry and leaf for the package we are validating.
                CatalogEntries = new[]
                {
                    new CatalogIndexEntry(
                        new Uri("https://nuget.test/catalog/leaf.json"),
                        CatalogConstants.NuGetPackageDetails,
                        Guid.NewGuid().ToString(),
                        DateTime.UtcNow,
                        PackageIdentity)
                };

                ValidatorTestUtility.AddCatalogLeafToMockServer(MockServer, new Uri("/catalog/leaf.json", UriKind.Relative), new CatalogLeaf
                {
                    Created = PackageCreationTime,
                    LastEdited = PackageCreationTime
                });
            }

            public void MakeVersionVisibleInSearch(
                string version = null)
            {
                MockServer.SetAction(
                    $"/query?q=packageid:{PackageIdentity.Id}&skip=0&take=1&prerelease=true&semVerLevel=2.0.0",
                    request =>
                    {
                        var json = JsonConvert.SerializeObject(new
                        {
                            data = new[]
                            {
                                new
                                {
                                    id = PackageIdentity.Id,
                                    version = version ?? PackageIdentity.Version.ToNormalizedString(),
                                    versions = new[]
                                    {
                                        new
                                        {
                                            version = version ?? PackageIdentity.Version.ToNormalizedString(),
                                            downloads = 0,
                                        }
                                    }
                                }
                            }
                        });

                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(json, Encoding.UTF8, "application/json"),
                        };

                        return Task.FromResult(response);
                    });
            }

            public void MakeEmptyResultInSearch(
                string version = null)
            {
                MockServer.SetAction(
                    $"/query?q=packageid:{PackageIdentity.Id}&skip=0&take=1&prerelease=true&semVerLevel=2.0.0",
                    request =>
                    {
                        var json = JsonConvert.SerializeObject(new
                        {
                            data = new object[0]
                        });

                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(json, Encoding.UTF8, "application/json"),
                        };

                        return Task.FromResult(response);
                    });
            }

            protected SearchHasVersionValidator CreateTarget()
            {
                var endpoint = ValidatorTestUtility.CreateSearchEndpoint();
                var logger = Mock.Of<ILogger<SearchHasVersionValidator>>();
                var config = ValidatorTestUtility.CreateValidatorConfig();

                return new SearchHasVersionValidator(endpoint, config, logger);
            }

            protected ValidationContext CreateValidationContext()
            {
                var timestamp = PackageTimestampMetadata.CreateForExistingPackage(created: PackageCreationTime, lastEdited: PackageCreationTime);
                var timestampMetadataResource = new Mock<IPackageTimestampMetadataResource>();
                timestampMetadataResource.Setup(t => t.GetAsync(It.IsAny<ValidationContext>())).ReturnsAsync(timestamp);

                var v2Resource = new Mock<IPackageRegistrationMetadataResource>();
                v2Resource
                    .Setup(x => x.GetIndexAsync(
                        It.IsAny<PackageIdentity>(),
                        It.IsAny<NuGet.Common.ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => DatabaseIndex);

                return ValidationContextStub.Create(
                    PackageIdentity,
                    CatalogEntries,
                    clientHandler: MockServer,
                    timestampMetadataResource: timestampMetadataResource.Object,
                    v2Resource: v2Resource.Object);
            }
        }
    }
}