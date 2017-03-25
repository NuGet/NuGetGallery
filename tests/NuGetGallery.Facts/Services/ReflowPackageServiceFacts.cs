// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class ReflowPackageServiceFacts
    {
        private static readonly string _packageHashForTests = "NzMzMS1QNENLNEczSDQ1SA==";

        private static ReflowPackageService CreateService(
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<PackageService> packageService = null,
            Mock<IPackageFileService> packageFileService = null,
            Action<Mock<ReflowPackageService>> setup = null)
        {
            var dbContext = new Mock<DbContext>();
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            entitiesContext.Setup(m => m.GetDatabase()).Returns(dbContext.Object.Database);

            packageService = packageService ?? new Mock<PackageService>();
            packageFileService = packageFileService ?? new Mock<IPackageFileService>();

            var reflowPackageService = new Mock<ReflowPackageService>(
                entitiesContext.Object,
                packageService.Object,
                packageFileService.Object);

            reflowPackageService.CallBase = true;

            if (setup != null)
            {
                setup(reflowPackageService);
            }

            return reflowPackageService.Object;
        }

        public class TheReflowAsyncMethod
        {
            [Fact]
            public async Task ReturnsNullWhenPackageNotFound()
            {
                // Arrange
                var package = CreateTestPackage();

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("unknownpackage", "1.0.0");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public async Task RetrievesOriginalPackageBinary()
            {
                // Arrange
                var package = CreateTestPackage();

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                await service.ReflowAsync("test", "1.0.0");

                // Assert
                packageFileService.Verify();
            }

            [Fact]
            public async Task RetrievesOriginalPackageMetadata()
            {
                // Arrange
                var package = CreateTestPackage();

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                await service.ReflowAsync("test", "1.0.0");

                // Assert
                packageService.Verify();
            }

            [Fact]
            public async Task RemovesOriginalFrameworks_Authors_Dependencies()
            {
                // Arrange
                var package = CreateTestPackage();

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                await service.ReflowAsync("test", "1.0.0");

                // Assert
                entitiesContext.Verify();
            }

            [Fact]
            public async Task UpdatesPackageMetadata()
            {
                // Arrange
                var package = CreateTestPackage();

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                Assert.Equal("test", result.PackageRegistration.Id);
                Assert.Equal("1.0.0", result.Version);
                Assert.Equal("1.0.0", result.NormalizedVersion);
                Assert.Equal("Test package", result.Title);

                Assert.Equal(2, result.Authors.Count);
                Assert.True(result.Authors.Any(a => a.Name == "authora"));
                Assert.True(result.Authors.Any(a => a.Name == "authorb"));
                Assert.Equal("authora, authorb", result.FlattenedAuthors);

                Assert.Equal(false, result.RequiresLicenseAcceptance);
                Assert.Equal("package A description.", result.Description);
                Assert.Equal("en-US", result.Language);

                Assert.Equal("WebActivator:[1.1.0, ):net40|PackageC:[1.1.0, 2.0.1):net40|jQuery:(, ):net451", result.FlattenedDependencies);
                Assert.Equal(3, result.Dependencies.Count);

                Assert.True(result.Dependencies.Any(d =>
                    d.Id == "WebActivator"
                    && d.VersionSpec == "[1.1.0, )"
                    && d.TargetFramework == "net40"));

                Assert.True(result.Dependencies.Any(d =>
                    d.Id == "PackageC"
                    && d.VersionSpec == "[1.1.0, 2.0.1)"
                    && d.TargetFramework == "net40"));

                Assert.True(result.Dependencies.Any(d =>
                    d.Id == "jQuery"
                    && d.VersionSpec == "(, )"
                    && d.TargetFramework == "net451"));

                Assert.Equal(0, result.SupportedFrameworks.Count);
            }

            [Fact]
            public async Task UpdatesPackageLastEdited()
            {
                // Arrange
                var package = CreateTestPackage();
                var lastEdited = package.LastEdited;

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                Assert.NotEqual(lastEdited, result.LastEdited);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task DoesNotUpdatePackageListed(bool listed)
            {
                // Arrange
                var package = CreateTestPackage();
                package.Listed = listed;

                var packageService = SetupPackageService(package);
                var entitiesContext = SetupEntitiesContext();
                var packageFileService = SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                Assert.Equal(listed, result.Listed);
            }
        }

        private static Package CreateTestPackage()
        {
            var packageRegistration = new PackageRegistration();
            packageRegistration.Id = "test";

            var framework = new PackageFramework();
            var author = new PackageAuthor { Name = "maarten" };
            var dependency = new PackageDependency { Id = "other" };

            var package = new Package
            {
                Key = 123,
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Hash = _packageHashForTests,
                SupportedFrameworks = new List<PackageFramework>
                {
                    framework
                },
                FlattenedAuthors = "maarten",
                Authors = new List<PackageAuthor>
                {
                    author
                },
                Dependencies = new List<PackageDependency>
                {
                    dependency
                },
                User = new User("test")
            };

            packageRegistration.Packages.Add(package);

            return package;
        }

        private static Mock<PackageService> SetupPackageService(Package package)
        {
            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            var packageRepository = new Mock<IEntityRepository<Package>>();
            var packageOwnerRequestRepo = new Mock<IEntityRepository<PackageOwnerRequest>>();
            var indexingService = new Mock<IIndexingService>();
            var packageNamingConflictValidator = new PackageNamingConflictValidator(
                    packageRegistrationRepository.Object,
                    packageRepository.Object);
            var auditingService = new TestAuditingService();

            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                packageOwnerRequestRepo.Object,
                indexingService.Object,
                packageNamingConflictValidator,
                auditingService);

            packageService.CallBase = true;

            packageService
                .Setup(s => s.FindPackageByIdAndVersion("test", "1.0.0", true))
                .Returns(package)
                .Verifiable();

            packageService
              .Setup(s => s.EnrichPackageFromNuGetPackage(
                  It.IsAny<Package>(),
                  It.IsAny<PackageArchiveReader>(),
                  It.IsAny<PackageMetadata>(),
                  It.IsAny<PackageStreamMetadata>(),
                  It.IsAny<User>()))
              .CallBase()
              .Verifiable();

            return packageService;
        }

        private static Mock<IEntitiesContext> SetupEntitiesContext()
        {
            var entitiesContext = new Mock<IEntitiesContext>();

            entitiesContext
                .Setup(s => s.Set<PackageFramework>().Remove(It.IsAny<PackageFramework>()))
                .Verifiable();

            entitiesContext
                .Setup(s => s.Set<PackageAuthor>().Remove(It.IsAny<PackageAuthor>()))
                .Verifiable();

            entitiesContext
                .Setup(s => s.Set<PackageDependency>().Remove(It.IsAny<PackageDependency>()))
                .Verifiable();

            return entitiesContext;
        }

        private static Mock<IPackageFileService> SetupPackageFileService(Package package)
        {
            var packageFileService = new Mock<IPackageFileService>();

            packageFileService
                .Setup(s => s.DownloadPackageFileAsync(package))
                .Returns(Task.FromResult(CreateTestPackageStream()))
                .Verifiable();

            return packageFileService;
        }

        private static Stream CreateTestPackageStream()
        {
            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
            {
                var nuspecEntry = packageArchive.CreateEntry("TestPackage.nuspec", CompressionLevel.Fastest);
                using (var streamWriter = new StreamWriter(nuspecEntry.Open()))
                {
                    streamWriter.WriteLine(@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>test</id>
                        <version>1.0.0</version>
                        <title>Test package</title>
                        <authors>authora, authorb</authors>
                        <owners>ownera</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <projectUrl>http://www.nuget.org/</projectUrl>
                        <iconUrl>http://www.nuget.org/</iconUrl>
                        <licenseUrl>http://www.nuget.org/</licenseUrl>
                        <dependencies>
                            <group targetFramework=""net40"">
                              <dependency id=""WebActivator"" version=""1.1.0"" />
                              <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                            </group>
                            <group targetFramework=""net451"">
                              <dependency id=""jQuery"" />
                            </group>
                        </dependencies>
                      </metadata>
                    </package>");
                }

                packageArchive.CreateEntry("content\\HelloWorld.cs", CompressionLevel.Fastest);
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}