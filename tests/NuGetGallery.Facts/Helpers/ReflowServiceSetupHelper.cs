// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using NuGetGallery.Packaging;
using NuGetGallery.Security;

namespace NuGetGallery.Helpers
{
    public static class ReflowServiceSetupHelper
    {
        public static Mock<PackageService> SetupPackageService(Package package)
        {
            var packageService = SetupPackageService();

            packageService
                .Setup(s => s.FindPackageByIdAndVersionStrict(package.Id, package.Version))
                .Returns(package)
                .Verifiable();

            return packageService;
        }

        public static void SetupPackages(Mock<PackageService> packageServiceMock, Mock<IPackageFileService> packageFileServiceMock, List<Package> packages)
        {
            foreach (var package in packages)
            {
                packageServiceMock
                    .Setup(s => s.FindPackageByIdAndVersionStrict(package.Id, package.Version))
                    .Returns(package)
                    .Verifiable();

                packageFileServiceMock
                    .Setup(s => s.DownloadPackageFileAsync(package))
                    .Returns(Task.FromResult(ReflowServiceSetupHelper.CreateTestPackageStream(CreateTestNuspec(package.Id), $"{package.Id}.nuspec")));
            }
        }

        public static void ThrowFindPackageByIdAndVersionStrict(this Mock<PackageService> packageServiceMock, List<Package> packages)
        {
            foreach (var package in packages)
            {
                packageServiceMock
                    .Setup(s => s.FindPackageByIdAndVersionStrict(package.Id, package.Version))
                    .Throws(new NotSupportedException("Unknown deprecation fields 'test'"));
            }
        }

        public static Mock<PackageService> SetupPackageService()
        {
            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            var packageRepository = new Mock<IEntityRepository<Package>>();
            var certificateRepository = new Mock<IEntityRepository<Certificate>>();
            var auditingService = new TestAuditingService();
            var telemetryService = new Mock<ITelemetryService>();
            var securityPolicyService = new Mock<ISecurityPolicyService>();
            var entitiesContext = new Mock<IEntitiesContext>();
            var contentObjectService = new Mock<IContentObjectService>();
            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService.Setup(x => x.ArePatternSetTfmHeuristicsEnabled()).Returns(true);


            var packageService = new Mock<PackageService>(
                packageRegistrationRepository.Object,
                packageRepository.Object,
                certificateRepository.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object,
                entitiesContext.Object,
                contentObjectService.Object,
                featureFlagService.Object);

            packageService.CallBase = true;

            packageService
              .Setup(s => s.EnrichPackageFromNuGetPackage(
                  It.IsAny<Package>(),
                  It.IsAny<PackageArchiveReader>(),
                  It.IsAny<PackageMetadata>(),
                  It.IsAny<PackageStreamMetadata>(),
                  It.IsAny<User>()))
              .CallBase()
              .Verifiable();

            packageService
                .Setup(s => s.UpdateIsLatestAsync(
                    It.IsAny<PackageRegistration>(),
                    It.IsAny<bool>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            return packageService;
        }

        public static Mock<IEntitiesContext> SetupEntitiesContext()
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

            entitiesContext
                .Setup(s => s.Set<PackageType>().Remove(It.IsAny<PackageType>()))
                .Verifiable();

            return entitiesContext;
        }

        public static Mock<IPackageFileService> SetupPackageFileService(Package package, Stream packageStream = null)
        {
            var packageFileService = new Mock<IPackageFileService>();

            packageFileService
                .Setup(s => s.DownloadPackageFileAsync(package))
                .Returns(Task.FromResult(packageStream ?? CreateTestPackageStream()))
                .Verifiable();

            return packageFileService;
        }

        public static string CreateTestNuspec(string id = "test")
        {
            return $@"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>{id}</id>
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
                              <dependency id=""jQuery"" version=""1.0.0""/>
                            </group>
                        </dependencies>
                      </metadata>
                    </package>";
        }

        public static Stream CreateTestPackageStream(string nuspec = null, string nuspecFilename = "TestPackage.nuspec")
        {
            if (string.IsNullOrEmpty(nuspec))
            {
                nuspec = CreateTestNuspec();
            }

            var packageStream = new MemoryStream();
            using (var packageArchive = new ZipArchive(packageStream, ZipArchiveMode.Create, true))
            {
                var nuspecEntry = packageArchive.CreateEntry(nuspecFilename, CompressionLevel.Fastest);
                using (var streamWriter = new StreamWriter(nuspecEntry.Open()))
                {
                    streamWriter.WriteLine(nuspec);
                }

                packageArchive.CreateEntry("content\\HelloWorld.cs", CompressionLevel.Fastest);
            }

            packageStream.Position = 0;

            return packageStream;
        }
    }
}
