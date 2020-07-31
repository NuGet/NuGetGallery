// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests.Validation
{
    public class PackageIsRepositorySignedValidatorFacts
    {
        public class Constructor
        {
            private readonly ValidatorConfiguration _configuration;
            private readonly FlatContainerEndpoint _endpoint;

            public Constructor()
            {
                _endpoint = ValidatorTestUtility.CreateFlatContainerEndpoint();
                _configuration = new ValidatorConfiguration(packageBaseAddress: "a", requireRepositorySignature: true);
            }

            [Fact]
            public void WhenEndpointIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new PackageIsRepositorySignedValidator(
                        endpoint: null,
                        config: _configuration,
                        logger: Mock.Of<ILogger<PackageIsRepositorySignedValidator>>()));

                Assert.Equal("endpoint", exception.ParamName);
            }

            [Fact]
            public void WhenConfigIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new PackageIsRepositorySignedValidator(
                        _endpoint,
                        config: null,
                        logger: Mock.Of<ILogger<PackageIsRepositorySignedValidator>>()));

                Assert.Equal("config", exception.ParamName);
            }

            [Fact]
            public void WhenLoggerIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new PackageIsRepositorySignedValidator(
                        _endpoint,
                        _configuration,
                        logger: null));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        public class ValidateAsync : FactsBase
        {
            [Theory]
            [InlineData((string)null)]
            [InlineData(UnsignedPackageResource)]
            [InlineData(AuthorSignedPackageResource)]
            [InlineData(RepoSignedPackageResource)]
            [InlineData(AuthorAndRepoSignedPackageResource)]
            public async Task SkipsIfConfigDoesNotRequireRepositorySignature(string packageResource)
            {
                // Arrange
                var target = CreateTarget(requireRepositorySignature: false);
                var context = CreateValidationContext(packageResource);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Skip, result.Result);
            }

            [Fact]
            public async Task SkipsIfPackageIsMissing()
            {
                // Arrange - modify the package ID on the validation context so that the
                // nupkg can no longer be found.
                var target = CreateTarget();
                var context = CreateValidationContext(packageResource: null);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Skip, result.Result);
                Assert.Null(result.Exception);
            }

            [Fact]
            public async Task FailsIfPackageHasNoSignature()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext(packageResource: UnsignedPackageResource);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                var exception = result.Exception as MissingRepositorySignatureException;

                Assert.Equal(TestResult.Fail, result.Result);
                Assert.NotNull(exception);

                Assert.Equal(MissingRepositorySignatureReason.Unsigned, exception.Reason);
                Assert.StartsWith("Package TestPackage 1.0.0 is unsigned.", exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageHasAnAuthorSignatureButNoRepositoryCountersignature()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext(AuthorSignedPackageResource);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                var exception = result.Exception as MissingRepositorySignatureException;

                Assert.Equal(TestResult.Fail, result.Result);
                Assert.NotNull(exception);

                Assert.Equal(MissingRepositorySignatureReason.AuthorSignedNoRepositoryCountersignature, exception.Reason);
                Assert.StartsWith("Package TestPackage 1.0.0 is author signed but not repository signed.", exception.Message);
            }

            [Fact]
            public async Task PassesIfPackageHasARepositoryPrimarySignature()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext(packageResource: RepoSignedPackageResource);

                // Act
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
                Assert.Null(result.Exception);
            }

            [Fact]
            public async Task PassesIfPackageHasARepositoryCountersignature()
            {
                // Arrange
                var context = CreateValidationContext(packageResource: AuthorAndRepoSignedPackageResource);

                // Act
                var target = CreateTarget();
                var result = await target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
                Assert.Null(result.Exception);
            }
        }

        public class FactsBase
        {
            public static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

            public const string UnsignedPackageResource = "Packages\\TestUnsigned.1.0.0.nupkg";
            public const string AuthorSignedPackageResource = "Packages\\TestSigned.leaf-1.1.0.0.nupkg";
            public const string RepoSignedPackageResource = "Packages\\TestRepoSigned.leaf-1.1.0.0.nupkg";
            public const string AuthorAndRepoSignedPackageResource = "Packages\\TestAuthorAndRepoSigned.leaf-1.1.0.0.nupkg";

            public static readonly DateTime PackageCreationTime = DateTime.UtcNow;

            private readonly IEnumerable<CatalogIndexEntry> _catalogEntries;
            private readonly MockServerHttpClientHandler _mockServer;

            public FactsBase()
            {
                _mockServer = new MockServerHttpClientHandler();

                // Mock a catalog entry and leaf for the package we are validating.
                _catalogEntries = new[]
                {
                    new CatalogIndexEntry(
                        new Uri("https://nuget.test/catalog/leaf.json"),
                        CatalogConstants.NuGetPackageDetails,
                        Guid.NewGuid().ToString(),
                        DateTime.UtcNow,
                        PackageIdentity)
                };

                ValidatorTestUtility.AddCatalogLeafToMockServer(_mockServer, new Uri("/catalog/leaf.json", UriKind.Relative), new CatalogLeaf
                {
                    Created = PackageCreationTime,
                    LastEdited = PackageCreationTime
                });
            }

            protected PackageIsRepositorySignedValidator CreateTarget(bool requireRepositorySignature = true)
            {
                var endpoint = ValidatorTestUtility.CreateFlatContainerEndpoint();
                var logger = Mock.Of<ILogger<PackageIsRepositorySignedValidator>>();
                var config = ValidatorTestUtility.CreateValidatorConfig(requireRepositorySignature: requireRepositorySignature);

                return new PackageIsRepositorySignedValidator(endpoint, config, logger);
            }

            protected ValidationContext CreateValidationContext(string packageResource = null)
            {
                // Add the package
                if (packageResource != null)
                {
                    var packageId = PackageIdentity.Id.ToLowerInvariant();
                    var packageVersion = PackageIdentity.Version.ToNormalizedString().ToLowerInvariant();
                    var relativeUrl = $"/packages/{packageId}/{packageVersion}/{packageId}.{packageVersion}.nupkg";
                    var bytes = File.ReadAllBytes(packageResource);

                    _mockServer.SetAction(
                        relativeUrl,
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(bytes)
                        }));
                }

                var httpClient = new CollectorHttpClient(_mockServer);

                // Mock V2 feed response for the package's Created/LastEdited timestamps. These timestamps must match
                // the mocked catalog entry's timestamps.
                var timestamp = PackageTimestampMetadata.CreateForExistingPackage(created: PackageCreationTime, lastEdited: PackageCreationTime);
                var timestampMetadataResource = new Mock<IPackageTimestampMetadataResource>();

                timestampMetadataResource.Setup(t => t.GetAsync(It.IsAny<ValidationContext>()))
                    .ReturnsAsync(timestamp);

                return ValidationContextStub.Create(
                    PackageIdentity,
                    _catalogEntries,
                    client: httpClient,
                    timestampMetadataResource: timestampMetadataResource.Object);
            }
        }
    }
}