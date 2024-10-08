// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class ReflowPackageServiceFacts
    {
        private static ReflowPackageService CreateService(
            Mock<PackageService> packageService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<ITelemetryService> telemetryService = null,
            Action<Mock<ReflowPackageService>> setup = null)
        {
            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();
            var database = new Mock<IDatabase>();
            database.Setup(x => x.BeginTransaction()).Returns(() => new Mock<IDbContextTransaction>().Object);
            entitiesContext.Setup(m => m.GetDatabase()).Returns(database.Object);

            packageService = packageService ?? new Mock<PackageService>();
            packageFileService = packageFileService ?? new Mock<IPackageFileService>();
            telemetryService = telemetryService ?? new Mock<ITelemetryService>();

            var reflowPackageService = new Mock<ReflowPackageService>(
                entitiesContext.Object,
                packageService.Object,
                packageFileService.Object,
                telemetryService.Object);

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
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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
            public async Task RemovesOriginalChildEntities()
            {
                // Arrange
                var package = PackageServiceUtility.CreateTestPackage();
                package.PackageTypes = new List<PackageType> { new PackageType { Name = "Dependency", Version = "0.0" } };

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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

#pragma warning disable 0618
                Assert.Equal(2, result.Authors.Count);
                Assert.Contains(result.Authors, a => a.Name == "authora");
                Assert.Contains(result.Authors, a => a.Name == "authorb");
#pragma warning restore 0618
                Assert.Equal("authora, authorb", result.FlattenedAuthors);

                Assert.False(result.RequiresLicenseAcceptance);
                Assert.Equal("package A description.", result.Description);
                Assert.Equal("en-US", result.Language);

                Assert.Equal("WebActivator:[1.1.0, ):net40|PackageC:[1.1.0, 2.0.1):net40|jQuery:[1.0.0, ):net451", result.FlattenedDependencies);
                Assert.Equal(3, result.Dependencies.Count);

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "WebActivator"
                    && d.VersionSpec == "[1.1.0, )"
                    && d.TargetFramework == "net40");

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "PackageC"
                    && d.VersionSpec == "[1.1.0, 2.0.1)"
                    && d.TargetFramework == "net40");

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "jQuery"
                    && d.VersionSpec == "[1.0.0, )"
                    && d.TargetFramework == "net451");

                Assert.Empty(result.SupportedFrameworks);
            }

            [Fact]
            public async Task UpdatesPackageLastEdited()
            {
                // Arrange
                var package = PackageServiceUtility.CreateTestPackage();
                var lastEdited = package.LastEdited;

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

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
                var package = PackageServiceUtility.CreateTestPackage();
                package.Listed = listed;

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                Assert.Equal(listed, result.Listed);
            }

            [Fact]
            public async Task CallsUpdateIsLatestAsync()
            {
                // Arrange
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                packageService.Verify(s => s.UpdateIsLatestAsync(package.PackageRegistration, false), Times.Once);
            }

            [Fact]
            public async Task EmitsTelemetry()
            {
                // Arrange
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(package);
                var telemetryService = new Mock<ITelemetryService>();

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService,
                    telemetryService: telemetryService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                telemetryService.Verify(
                    x => x.TrackPackageReflow(package),
                    Times.Once);
            }

            [Fact]
            public async Task AllowsInvalidPackageDependencyVersion()
            {
                // Arrange
                var package = PackageServiceUtility.CreateTestPackage();

                var packageService = ReflowServiceSetupHelper.SetupPackageService(package);
                var entitiesContext = ReflowServiceSetupHelper.SetupEntitiesContext();
                var packageFileService = ReflowServiceSetupHelper.SetupPackageFileService(
                    package,
                    CreateInvalidDependencyVersionTestPackageStream());

                var service = CreateService(
                    packageService: packageService,
                    entitiesContext: entitiesContext,
                    packageFileService: packageFileService);

                // Act
                var result = await service.ReflowAsync("test", "1.0.0");

                // Assert
                Assert.Equal("test", result.PackageRegistration.Id);
                Assert.Equal("1.0.0", result.NormalizedVersion);

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "WebActivator"
                    && d.VersionSpec == "(, )"
                    && d.TargetFramework == "net40");

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "PackageC"
                    && d.VersionSpec == "[1.1.0, 2.0.1)"
                    && d.TargetFramework == "net40");

                Assert.Contains(result.Dependencies, d =>
                    d.Id == "jQuery"
                    && d.VersionSpec == "(, )"
                    && d.TargetFramework == "net451");
            }
        }

        private static Stream CreateInvalidDependencyVersionTestPackageStream()
        {
            var nuspec = @"<?xml version=""1.0""?>
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
                              <dependency id=""WebActivator"" version="""" />
                              <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                            </group>
                            <group targetFramework=""net451"">
                              <dependency id=""jQuery"" version=""$version$""/>
                            </group>
                        </dependencies>
                      </metadata>
                    </package>";

            return ReflowServiceSetupHelper.CreateTestPackageStream(nuspec);
        }
    }
}