// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    public class PackageHasSignatureValidatorFacts
    {
        public class Constructor
        {
            private readonly ValidatorConfiguration _configuration;

            public Constructor()
            {
                _configuration = new ValidatorConfiguration(packageBaseAddress: "a", requireRepositorySignature: true);
            }

            [Fact]
            public void WhenConfigIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new PackageHasSignatureValidator(
                        config: null,
                        logger: Mock.Of<ILogger<PackageHasSignatureValidator>>()));

                Assert.Equal("config", exception.ParamName);
            }

            [Fact]
            public void WhenLoggerIsNull_Throws()
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new PackageHasSignatureValidator(
                        _configuration,
                        logger: null));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        public class ShouldRunValidator : FactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SkipsIfNoEntries(bool requirePackageSignature)
            {
                var target = CreateTarget(requirePackageSignature);
                var context = CreateValidationContext(catalogEntries: new CatalogIndexEntry[0]);

                Assert.False(target.ShouldRunValidator(context));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SkipsIfLatestEntryIsDelete(bool requirePackageSignature)
            {
                var target = CreateTarget(requirePackageSignature);
                var uri = new Uri($"https://nuget.test/{PackageIdentity.Id}");
                var context = CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri,
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                        new CatalogIndexEntry(
                            uri,
                            type: CatalogConstants.NuGetPackageDelete,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue.AddDays(1),
                            packageIdentity: PackageIdentity),
                    });

                Assert.False(target.ShouldRunValidator(context));
            }

            [Fact]
            public void SkipsIfLatestEntryIsNotDeleteAndPackageSignatureIsNotRequired()
            {
                var target = CreateTarget(requireRepositorySignature: false);
                var context = CreateValidationContextWithLatestEntryWithDetails();
                Assert.False(target.ShouldRunValidator(context));
            }

            [Fact]
            public void RunsIfLatestEntryIsNotDeleteAndPackageSignatureIsRequired()
            {
                var target = CreateTarget();
                var context = CreateValidationContextWithLatestEntryWithDetails();
                Assert.True(target.ShouldRunValidator(context));
            }

            private ValidationContext CreateValidationContextWithLatestEntryWithDetails()
            {
                var uri = new Uri($"https://nuget.test/{PackageIdentity.Id}");

                return CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri,
                            type: CatalogConstants.NuGetPackageDelete,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                        new CatalogIndexEntry(
                            uri,
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue.AddDays(1),
                            packageIdentity: PackageIdentity),
                    });
            }
        }

        public class RunValidatorAsync : FactsBase
        {
            [Fact]
            public async Task ReturnsGracefullyIfLatestLeafHasSignatureFile()
            {
                // Arrange
                var target = CreateTarget();
                var context = CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri: new Uri("https://nuget.test/a.json"),
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                        new CatalogIndexEntry(
                            uri: new Uri("https://nuget.test/b.json"),
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue.AddDays(1),
                            packageIdentity: PackageIdentity),
                    });

                AddCatalogLeaf("/a.json", new CatalogLeaf
                {
                    PackageEntries = new[]
                    {
                        new PackageEntry { FullName = "hello.txt" }
                    }
                });

                AddCatalogLeaf("/b.json", new CatalogLeaf
                {
                    PackageEntries = new[]
                    {
                        new PackageEntry { FullName = "hello.txt" },
                        new PackageEntry { FullName = ".signature.p7s" }
                    }
                });

                // Act & Assert
                await target.RunValidatorAsync(context);
            }

            [Fact]
            public async Task ThrowsIfLatestLeafIsMissingASignatureFile()
            {
                // Arrange
                var malformedUri = new Uri("https://nuget.test/b.json");

                var target = CreateTarget();
                var context = CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri: new Uri("https://nuget.test/a.json"),
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                        new CatalogIndexEntry(
                            uri: malformedUri,
                            type: CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue.AddDays(1),
                            packageIdentity: PackageIdentity),
                    });

                AddCatalogLeaf("/a.json", new CatalogLeaf
                {
                    PackageEntries = new[]
                    {
                        new PackageEntry { FullName = ".signature.p7s" }
                    }
                });

                AddCatalogLeaf("/b.json", new CatalogLeaf
                {
                    PackageEntries = new[]
                    {
                        new PackageEntry { FullName = "hello.txt" }
                    }
                });

                // Act & Assert
                var e = await Assert.ThrowsAsync<MissingPackageSignatureFileException>(() => target.RunValidatorAsync(context));

                Assert.Same(malformedUri, e.CatalogEntry);
            }

            [Fact]
            public async Task ThrowsIfLeafPackageEntriesIsMissing()
            {
                // Arrange
                var uri = new Uri("https://nuget.test/a.json");

                var target = CreateTarget();
                var context = CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri,
                            CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                    });

                AddCatalogLeaf("/a.json", "{ 'this': 'is missing the packageEntries field' }");

                // Act & Assert
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => target.RunValidatorAsync(context));

                Assert.Equal($"The catalog leaf at {uri.AbsoluteUri} is missing the 'packageEntries' property.", e.Message);
            }

            [Fact]
            public async Task ThrowsIfLeafPackageEntriesIsMalformed()
            {
                // Arrange
                var uri = new Uri("https://nuget.test/a.json");

                var target = CreateTarget();
                var context = CreateValidationContext(
                    catalogEntries: new[]
                    {
                        new CatalogIndexEntry(
                            uri,
                            CatalogConstants.NuGetPackageDetails,
                            commitId: Guid.NewGuid().ToString(),
                            commitTs: DateTime.MinValue,
                            packageIdentity: PackageIdentity),
                    });

                AddCatalogLeaf("/a.json", "{ 'packageEntries': 'malformed' }");

                // Act & Assert
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => target.RunValidatorAsync(context));

                Assert.Equal($"The catalog leaf at {uri.AbsoluteUri} has a malformed 'packageEntries' property.", e.Message);
            }
        }

        public class FactsBase
        {
            public static readonly PackageIdentity PackageIdentity = new PackageIdentity("TestPackage", NuGetVersion.Parse("1.0.0"));

            protected readonly Mock<ILogger<PackageHasSignatureValidator>> _logger;
            private readonly MockServerHttpClientHandler _mockServer;

            public FactsBase()
            {
                _logger = new Mock<ILogger<PackageHasSignatureValidator>>();
                _mockServer = new MockServerHttpClientHandler();
            }

            protected ValidationContext CreateValidationContext(IEnumerable<CatalogIndexEntry> catalogEntries = null)
            {
                catalogEntries = catalogEntries ?? new CatalogIndexEntry[0];

                var httpClient = new CollectorHttpClient(_mockServer);

                return ValidationContextStub.Create(
                    PackageIdentity,
                    catalogEntries,
                    client: httpClient);
            }

            protected PackageHasSignatureValidator CreateTarget(bool requireRepositorySignature = true)
            {
                var config = ValidatorTestUtility.CreateValidatorConfig(requireRepositorySignature: requireRepositorySignature);

                return new PackageHasSignatureValidator(config, _logger.Object);
            }

            protected void AddCatalogLeaf(string path, CatalogLeaf leaf)
            {
                ValidatorTestUtility.AddCatalogLeafToMockServer(_mockServer, new Uri(path, UriKind.Relative), leaf);
            }

            protected void AddCatalogLeaf(string path, string leafContent)
            {
                _mockServer.SetAction(path, request =>
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(leafContent)
                    });
                });
            }
        }
    }
}