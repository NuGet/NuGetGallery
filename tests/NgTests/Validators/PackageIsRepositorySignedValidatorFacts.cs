// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;
using Xunit;

namespace NgTests
{
    public class PackageIsRepositorySignedValidatorFacts
    {
        public class Validate : FactsBase
        {
            [Fact]
            public async Task FailsIfPackageIsMissing()
            {
                // Arrange - modify the package ID on the validation context so that the
                // nupkg can no longer be found.
                var context = CreateValidationContext(packageResource: null);

                // Act
                var result = await _target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Fail, result.Result);
                Assert.NotNull(result.Exception);
                Assert.Contains("Package TestPackage 1.0.0 couldn't be downloaded at https://localhost/packages/testpackage/1.0.0/testpackage.1.0.0.nupkg", result.Exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageHasNoSignature()
            {
                // Arrange
                var context = CreateValidationContext(packageResource: UnsignedPackageResource);

                // Act
                var result = await _target.ValidateAsync(context);

                // Assert
                var exception = result.Exception as MissingRepositorySignatureException;

                Assert.Equal(TestResult.Fail, result.Result);
                Assert.NotNull(exception);

                Assert.Equal(MissingRepositorySignatureReason.Unsigned, exception.Reason);
                Assert.Contains("Package TestPackage 1.0.0 is unsigned", exception.Message);
            }

            [Fact]
            public async Task FailsIfPackageHasAnAuthorSignatureButNoRepositoryCountersignature()
            {
                // Arrange
                var context = CreateValidationContext(packageResource: AuthorSignedPackageResource);

                // Act
                var result = await _target.ValidateAsync(context);

                // Assert
                var exception = result.Exception as MissingRepositorySignatureException;

                Assert.Equal(TestResult.Fail, result.Result);
                Assert.NotNull(exception);

                Assert.Equal(MissingRepositorySignatureReason.AuthorSignedNoRepositoryCountersignature, exception.Reason);
                Assert.Contains("Package TestPackage 1.0.0 is author signed but not repository signed", exception.Message);
            }

            [Fact]
            public async Task PassesIfPackageHasARepositoryPrimarySignature()
            {
                // Arrange
                var context = CreateValidationContext(packageResource: RepoSignedPackageResource);

                // Act
                var result = await _target.ValidateAsync(context);

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
                var result = await _target.ValidateAsync(context);

                // Assert
                Assert.Equal(TestResult.Pass, result.Result);
                Assert.Null(result.Exception);
            }
        }

        public class FactsBase
        {
            public const string PackageId = "TestPackage";
            public const string PackageVersion = "1.0.0";

            public const string UnsignedPackageResource = "Packages\\TestUnsigned.1.0.0.nupkg";
            public const string AuthorSignedPackageResource = "Packages\\TestSigned.leaf-1.1.0.0.nupkg";
            public const string RepoSignedPackageResource = "Packages\\TestRepoSigned.leaf-1.1.0.0.nupkg";
            public const string AuthorAndRepoSignedPackageResource = "Packages\\TestAuthorAndRepoSigned.leaf-1.1.0.0.nupkg";

            public static readonly DateTime PackageCreationTime = DateTime.UtcNow;
            public static readonly NuGetVersion PackageNuGetVersion = NuGetVersion.Parse(PackageVersion);

            private readonly IEnumerable<CatalogIndexEntry> _catalogEntries;

            private readonly Mock<SourceRepository> _source;
            private readonly MockServerHttpClientHandler _mockServer;

            protected readonly PackageIsRepositorySignedValidator _target;

            public FactsBase()
            {
                var feedToSource = new Mock<IDictionary<FeedType, SourceRepository>>();
                var timestampResource = new Mock<IPackageTimestampMetadataResource>();
                var logger = Mock.Of<ILogger<PackageIsRepositorySignedValidator>>();

                _source = new Mock<SourceRepository>();
                _mockServer = new MockServerHttpClientHandler();

                feedToSource.Setup(x => x[It.IsAny<FeedType>()]).Returns(_source.Object);

                // Mock a catalog entry and leaf for the package we are validating.
                _catalogEntries = new[]
                {
                    new CatalogIndexEntry(
                        new Uri("https://localhost/catalog/leaf.json"),
                        string.Empty,
                        string.Empty,
                        DateTime.UtcNow,
                        PackageId,
                        PackageNuGetVersion)
                };

                AddCatalogLeafToMockServer("/catalog/leaf.json", new CatalogLeaf
                {
                    Created = PackageCreationTime,
                    LastEdited = PackageCreationTime,
                });

                // Mock V2 feed response for the package's Created/LastEdited timestamps. These timestamps must match
                // the mocked catalog entry's timestamps.
                var timestamp = PackageTimestampMetadata.CreateForPackageExistingOnFeed(created: PackageCreationTime, lastEdited: PackageCreationTime);
                timestampResource.Setup(t => t.GetAsync(It.IsAny<ValidationContext>())).ReturnsAsync(timestamp);
                _source.Setup(s => s.GetResource<IPackageTimestampMetadataResource>()).Returns(timestampResource.Object);

                // Add the package base address resource
                var resource = new PackageBaseAddressResource("https://localhost/packages/");
                _source.Setup(s => s.GetResource<PackageBaseAddressResource>()).Returns(resource);

                _target = new PackageIsRepositorySignedValidator(feedToSource.Object, logger);
            }

            protected ValidationContext CreateValidationContext(string packageResource = null)
            {
                // Add the package
                if (packageResource != null)
                {
                    var resourceStream = File.OpenRead(packageResource);

                    _mockServer.SetAction(
                        $"/packages/testpackage/1.0.0/testpackage.1.0.0.nupkg",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StreamContent(resourceStream)
                        }));
                }

                // Create the validation context.
                var httpClient = new CollectorHttpClient(_mockServer);

                return new ValidationContext(
                    new PackageIdentity(PackageId, PackageNuGetVersion),
                    _catalogEntries,
                    new DeletionAuditEntry[0],
                    httpClient,
                    CancellationToken.None);
            }

            private void AddCatalogLeafToMockServer(string path, CatalogLeaf leaf)
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                _mockServer.SetAction(path, request =>
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(leaf, jsonSettings))
                    });
                });
            }

            public class CatalogLeaf
            {
                public DateTimeOffset Created { get; set; }
                public DateTimeOffset LastEdited { get; set; }

                public IEnumerable<PackageEntry> PackageEntries { get; set; }
            }
        }
    }
}
