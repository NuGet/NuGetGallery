// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackageUploadServiceFacts
    {
        private const string Id = "NuGet.Versioning";
        private const string Version = "3.4.0.0-ALPHA+1";

        public Mock<IPackageService> MockPackageService { get; private set; }

        private static PackageUploadService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IValidationService> validationService = null,
            Mock<IPackageVulnerabilitiesManagementService> vulnerabilityService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();

            packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
            packageService.Setup(x => x
                .CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<User>(), It.IsAny<bool>()))
                .Returns((PackageArchiveReader packageArchiveReader, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser, bool isVerified) =>
                {
                    var packageMetadata = PackageMetadata.FromNuspecReader(
                        packageArchiveReader.GetNuspecReader(),
                        strict: true);

                    var newPackage = new Package();
                    newPackage.PackageRegistration = new PackageRegistration { Id = packageMetadata.Id, IsVerified = isVerified };
                    newPackage.Version = packageMetadata.Version.ToString();
                    newPackage.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());

                    return Task.FromResult(newPackage);
                });

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(Array.Empty<ReservedNamespace>());
            }

            if (vulnerabilityService == null)
            {
                vulnerabilityService = new Mock<IPackageVulnerabilitiesManagementService>();
            }

            validationService = validationService ?? new Mock<IValidationService>();
            var diagnosticsService = new Mock<IDiagnosticsService>();
            diagnosticsService
                .Setup(ds => ds.GetSource(It.IsAny<string>()))
                .Returns(Mock.Of<IDiagnosticsSource>());
            var metadataValidationService = new Mock<IPackageMetadataValidationService>();

            var packageUploadService = new Mock<PackageUploadService>(
                packageService.Object,
                new Mock<IPackageFileService>().Object,
                new Mock<IEntitiesContext>().Object,
                reservedNamespaceService.Object,
                validationService.Object,
                Mock.Of<ICoreLicenseFileService>(),
                Mock.Of<ICoreReadmeFileService>(),
                diagnosticsService.Object,
                vulnerabilityService.Object,
                metadataValidationService.Object);

            return packageUploadService.Object;
        }

        public class TheGeneratePackageAsyncMethod
        {
            [Fact]
            public async Task WillCallCreatePackageAsyncCorrectly()
            {
                var key = 0;
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
                var vulnerabilityService = new Mock<IPackageVulnerabilitiesManagementService>();

                var id = "Microsoft.Aspnet.Mvc";
                var packageUploadService = CreateService(packageService, vulnerabilityService: vulnerabilityService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);
                var owner = new User { Key = key++, Username = "owner" };
                var currentUser = new User { Key = key++, Username = "user" };

                var package = await packageUploadService.GeneratePackageAsync(
                    id, nugetPackage.Object, new PackageStreamMetadata(), owner, currentUser);

                packageService.Verify(
                    x => x.CreatePackageAsync(
                        It.IsAny<PackageArchiveReader>(), 
                        It.IsAny<PackageStreamMetadata>(), 
                        owner, 
                        currentUser, 
                        false), 
                    Times.Once);

                vulnerabilityService.Verify(
                    x => x.ApplyExistingVulnerabilitiesToPackage(package),
                    Times.Once);

                Assert.False(package.PackageRegistration.IsVerified);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WillMarkPackageRegistrationVerifiedFlagCorrectly(bool shouldMarkIdVerified)
            {
                var id = "Microsoft.Aspnet.Mvc";
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefixes = new List<string> { "microsoft.", "microsoft.aspnet." };
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var matchingNamepsaces = testNamespaces
                    .Where(rn => prefixes.Any(pr => id.StartsWith(pr, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                prefixes.ForEach(p =>
                {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = matchingNamepsaces;
                reservedNamespaceService.Setup(s => s.ShouldMarkNewPackageIdVerified(It.IsAny<User>(), It.IsAny<string>(), out userOwnedMatchingNamespaces))
                    .Returns(shouldMarkIdVerified);

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(testNamespaces.ToList().AsReadOnly());

                var vulnerabilityService = new Mock<IPackageVulnerabilitiesManagementService>();

                var packageUploadService = CreateService(
                    reservedNamespaceService: reservedNamespaceService, 
                    vulnerabilityService: vulnerabilityService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), firstUser, firstUser);

                vulnerabilityService.Verify(
                    x => x.ApplyExistingVulnerabilitiesToPackage(package),
                    Times.Once);

                Assert.Equal(shouldMarkIdVerified, package.PackageRegistration.IsVerified);
            }

            [Fact]
            public async Task WillMarkPackageRegistrationNotVerifiedIfIdMatchesNonOwnedSharedNamespace()
            {
                var id = "Microsoft.Aspnet.Mvc";
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefixes = new List<string> { "microsoft.", "microsoft.aspnet." };
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                prefixes.ForEach(p =>
                {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.IsSharedNamespace = true;
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService
                    .Setup(s => s.ShouldMarkNewPackageIdVerified(It.IsAny<User>(), It.IsAny<string>(), out userOwnedMatchingNamespaces))
                    .Returns(false);

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(testNamespaces.ToList().AsReadOnly());

                var vulnerabilityService = new Mock<IPackageVulnerabilitiesManagementService>();

                var packageUploadService = CreateService(
                    reservedNamespaceService: reservedNamespaceService,
                    vulnerabilityService: vulnerabilityService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), lastUser, lastUser);

                vulnerabilityService.Verify(
                    x => x.ApplyExistingVulnerabilitiesToPackage(package),
                    Times.Once);

                Assert.False(package.PackageRegistration.IsVerified);
            }
        }

        public class TheCommitPackageMethod : FactsBase
        {
            public static IEnumerable<object[]> SupportedPackageStatuses => new[]
            {
                new object[] { PackageStatus.Available },
                new object[] { PackageStatus.Validating },
            };

            public static IEnumerable<object[]> UnsupportedPackageStatuses => Enum
                .GetValues(typeof(PackageStatus))
                .Cast<PackageStatus>()
                .Concat(new[] { (PackageStatus)(-1) })
                .Where(s => !SupportedPackageStatuses.Any(o => s.Equals(o[0])))
                .Select(s => new object[] { s });

            [Fact]
            public async Task ThrowsWhenPackageIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.CommitPackageAsync(package: null, packageFile: Mock.Of<Stream>()));
                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public async Task ThrowsWhenPackageFileIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.CommitPackageAsync(package: Mock.Of<Package>(), packageFile: null));
                Assert.Equal("packageFile", ex.ParamName);
            }

            [Fact]
            public async Task ThrowsWhenPackageStreamIsNotSeekable()
            {
                var stream = new Mock<Stream>();
                stream.SetupGet(s => s.CanSeek)
                    .Returns(false);

                var ex = await Assert.ThrowsAsync<ArgumentException>(() => _target.CommitPackageAsync(package: Mock.Of<Package>(), packageFile: stream.Object));
                Assert.Contains("seek", ex.Message);
                Assert.Equal("packageFile", ex.ParamName);
            }

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task CommitsAfterSavingSupportedPackageStatuses(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = PackageStatus.FailedValidation;

                _validationService
                    .Setup(vs => vs.UpdatePackageAsync(_package))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _package.PackageStatusKey = packageStatus);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Theory]
            [MemberData(nameof(UnsupportedPackageStatuses))]
            public async Task RejectsUnsupportedPackageStatuses(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = PackageStatus.Available;

                _validationService
                    .Setup(vs => vs.UpdatePackageAsync(_package))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _package.PackageStatusKey = packageStatus);

                await Assert.ThrowsAsync<ArgumentException>(
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task StartsAsynchronousValidation(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = packageStatus;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _validationService.Verify(
                    x => x.StartValidationAsync(_package),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationAsync(It.IsAny<Package>()),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task StartsValidationAfterSavingPackage(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = packageStatus;

                bool contextSaveDone = false;
                bool packageSaved = false;
                _validationService
                    .Setup(vs => vs.StartValidationAsync(_package))
                    .Returns(Task.CompletedTask)
                    .Callback(() => Assert.True(packageSaved && !contextSaveDone));

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .Returns(Task.FromResult(1))
                    .Callback(() => contextSaveDone = true);
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => packageSaved = true);
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => packageSaved = true);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _validationService
                    .Verify(vs => vs.StartValidationAsync(_package),
                    Times.AtLeastOnce);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
            }

            [Fact]
            public async Task SavesPackageToStorageAndDatabaseWhenAvailable()
            {
                _package.PackageStatusKey = PackageStatus.Available;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(_package, _packageFile),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Fact]
            public async Task SavesPackageToStorageAndDatabaseWhenValidating()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(_package, _packageFile),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenSavingTheFileFails()
            {
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenThePackageFileConflicts()
            {
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_conflictException);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenTheValidationFileConflicts()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _packageFileService
                    .Setup(x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_conflictException);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Fact]
            public async Task DeletesPackageIfDatabaseCommitFailsWhenAvailable()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                _package.NormalizedVersion = "3.2.1";

                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task ReturnsConflictWhenDBCommitThrowsConcurrencyViolations()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                var ex = new DbUpdateConcurrencyException("whoops!");
                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(ex);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);

                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Fact]
            public async Task DeletesPackageIfDatabaseCommitFailsWhenValidating()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task RejectsUploadWhenValidatingAndPackageExistsInPackagesContainer()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _packageFileService
                    .Setup(x => x.DoesPackageFileExistAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                _packageFileService.Verify(
                    x => x.DoesPackageFileExistAsync(It.IsAny<Package>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);

                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task SavesLicenseFileWhenPackageIsAvailable(PackageStatus packageStatus, bool expectedLicenseSave)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _licenseFileService.Verify(
                    lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile),
                    expectedLicenseSave ? Times.Once() : Times.Never());
            }

            [Fact]
            public async Task ResetsPackageStreamAfterSavingLicenseFile()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _licenseFileService
                    .Setup(lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile))
                    .Callback<Package, Stream>((_, stream) => stream.Position = 42)
                    .Returns(Task.CompletedTask);

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, _packageFile))
                    .Callback<Package, Stream>((_, stream) => Assert.Equal(0, stream.Position))
                    .Returns(Task.CompletedTask);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _licenseFileService.Verify(lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile), Times.Once);
                _packageFileService.Verify(pfs => pfs.SavePackageFileAsync(_package, _packageFile), Times.Once);
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpLicenseIfBlobSaveFails(PackageStatus packageStatus, bool expectedLicenseDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion),
                    expectedLicenseDelete ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpLicenseIfDbUpdateFails(PackageStatus packageStatus, bool expectedLicenseDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion.ToString()),
                    expectedLicenseDelete ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task SavesReadmeFileWhenPackageIsAvailable(PackageStatus packageStatus, bool expectedReadmeSave)
            {
                _package.PackageStatusKey = packageStatus;
                _package.HasReadMe = true;
                _package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;

                _packageFile = GeneratePackageWithUserContent(readmeFilename: "read.md", readmeFileContents: "readme test").Object.GetStream();

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _readmeFileService.Verify(
                    lfs => lfs.ExtractAndSaveReadmeFileAsync(_package, _packageFile),
                    expectedReadmeSave ? Times.Once() : Times.Never());
            }

            [Fact]
            public async Task ResetsPackageStreamAfterSavingReadmeFile()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.HasReadMe = true;
                _package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license", readmeFilename: "read.md", readmeFileContents: "readme test").Object.GetStream();

                _licenseFileService
                    .Setup(lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile))
                    .Callback<Package, Stream>((_, stream) => stream.Position = 42)
                    .Returns(Task.CompletedTask);

                _readmeFileService
                    .Setup(lfs => lfs.ExtractAndSaveReadmeFileAsync(_package, _packageFile))
                    .Callback<Package, Stream>((_, stream) => stream.Position = 42)
                    .Returns(Task.CompletedTask);

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, _packageFile))
                    .Callback<Package, Stream>((_, stream) => Assert.Equal(0, stream.Position))
                    .Returns(Task.CompletedTask);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _licenseFileService.Verify(lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile), Times.Once);
                _readmeFileService.Verify(lfs => lfs.ExtractAndSaveReadmeFileAsync(_package, _packageFile), Times.Once);
                _packageFileService.Verify(pfs => pfs.SavePackageFileAsync(_package, _packageFile), Times.Once);
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpReadmeIfBlobSaveFails(PackageStatus packageStatus, bool expectedReadmeDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";
                _package.HasReadMe = true;
                _package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license", readmeFilename: "read.md", readmeFileContents: "readme test").Object.GetStream();

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion),
                    expectedReadmeDelete ? Times.Once() : Times.Never());

                _readmeFileService.Verify(
                    lfs => lfs.DeleteReadmeFileAsync(_package.Id, _package.NormalizedVersion),
                    expectedReadmeDelete ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpReadmeIfDbUpdateFails(PackageStatus packageStatus, bool expectedFileDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";
                _package.HasReadMe = true;
                _package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;

                _packageFile = GeneratePackageWithUserContent(licenseFilename: "license.txt", licenseFileContents: "some license", readmeFilename: "read.md", readmeFileContents: "readme test").Object.GetStream();

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion.ToString()),
                    expectedFileDelete ? Times.Once() : Times.Never());

                _readmeFileService.Verify(
                    lfs => lfs.DeleteReadmeFileAsync(_package.Id, _package.NormalizedVersion.ToString()),
                    expectedFileDelete ? Times.Once() : Times.Never());
            }
        }

        public abstract class FactsBase
        {
            protected const string PackageId = "theId";
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageFileService> _packageFileService;
            protected readonly Mock<IEntitiesContext> _entitiesContext;
            protected readonly Mock<IReservedNamespaceService> _reservedNamespaceService;
            protected readonly Mock<IValidationService> _validationService;
            protected readonly Mock<IAppConfiguration> _config;
            protected readonly Mock<ITyposquattingService> _typosquattingService;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<ICoreLicenseFileService> _licenseFileService;
            protected readonly Mock<ICoreReadmeFileService> _readmeFileService;
            protected readonly Mock<IDiagnosticsService> _diagnosticsService;
            protected readonly Mock<IPackageVulnerabilitiesManagementService> _vulnerabilityService;
            protected readonly Mock<IPackageMetadataValidationService> _metadataValidationService;
            protected Package _package;
            protected Stream _packageFile;
            protected ArgumentException _unexpectedException;
            protected FileAlreadyExistsException _conflictException;
            protected readonly CancellationToken _token;
            protected readonly Mock<IFeatureFlagService> _featureFlagService;
            protected readonly PackageUploadService _target;

            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _packageFileService = new Mock<IPackageFileService>();
                _entitiesContext = new Mock<IEntitiesContext>();
                _reservedNamespaceService = new Mock<IReservedNamespaceService>();
                _validationService = new Mock<IValidationService>();
                _config = new Mock<IAppConfiguration>();
                _config
                    .SetupGet(x => x.AllowLicenselessPackages)
                    .Returns(true);

                _typosquattingService = new Mock<ITyposquattingService>();

                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = Id,
                    },
                    Version = Version,
                };
                _packageFile = Stream.Null;
                _unexpectedException = new ArgumentException("Fail!");
                _conflictException = new FileAlreadyExistsException("Conflict!");
                _token = CancellationToken.None;
                _licenseFileService = new Mock<ICoreLicenseFileService>();
                _readmeFileService = new Mock<ICoreReadmeFileService>();
                _diagnosticsService = new Mock<IDiagnosticsService>();

                _diagnosticsService
                    .Setup(ds => ds.GetSource(It.IsAny<string>()))
                    .Returns(Mock.Of<IDiagnosticsSource>());

                _vulnerabilityService = new Mock<IPackageVulnerabilitiesManagementService>();

                _metadataValidationService = new Mock<IPackageMetadataValidationService>();

                _target = new PackageUploadService(
                    _packageService.Object,
                    _packageFileService.Object,
                    _entitiesContext.Object,
                    _reservedNamespaceService.Object,
                    _validationService.Object,
                    _licenseFileService.Object,
                    _readmeFileService.Object,
                    _diagnosticsService.Object,
                    _vulnerabilityService.Object,
                    _metadataValidationService.Object);
            }

            protected static Mock<TestPackageReader> GeneratePackage(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                IReadOnlyList<string> entryNames = null,
                Func<string> getCustomNuspecNodes = null)
                => GeneratePackageWithUserContent(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"),
                    licenseExpression: "MIT",
                    licenseFilename: null,
                    licenseFileContents: null,
                    licenseFileBinaryContents: null,
                    entryNames: entryNames);

            protected static Mock<TestPackageReader> GeneratePackageWithUserContent(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null,
                Uri iconUrl = null,
                Uri licenseUrl = null,
                string licenseExpression = null,
                string licenseFilename = null,
                string licenseFileContents = null,
                byte[] licenseFileBinaryContents = null,
                string iconFilename = null,
                byte[] iconFileBinaryContents = null,
                string readmeFilename = null,
                string readmeFileContents = null,
                byte[] readmeFileBinaryContents = null,
                IReadOnlyList<string> entryNames = null)
            {
                var packageStream = GeneratePackageStream(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    iconUrl: iconUrl,
                    licenseUrl: licenseUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents,
                    licenseFileBinaryContents: licenseFileBinaryContents,
                    iconFilename: iconFilename,
                    iconFileBinaryContents: iconFileBinaryContents,
                    readmeFilename: readmeFilename,
                    readmeFileContents: readmeFileContents,
                    readmeFileBinaryContents: readmeFileBinaryContents,
                    entryNames: entryNames);

                return PackageServiceUtility.CreateNuGetPackage(packageStream);
            }

            protected static MemoryStream GeneratePackageStream(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null,
                Uri iconUrl = null,
                Uri licenseUrl = null,
                string licenseExpression = null,
                string licenseFilename = null,
                string licenseFileContents = null,
                byte[] licenseFileBinaryContents = null,
                string iconFilename = null,
                byte[] iconFileBinaryContents = null,
                string readmeFilename = null,
                string readmeFileContents = null,
                byte[] readmeFileBinaryContents = null,
                IReadOnlyList<string> entryNames = null)
            {
                return PackageServiceUtility.CreateNuGetPackageStream(
                    id: PackageId,
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: licenseUrl,
                    iconUrl: iconUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: GetBinaryFileContents(licenseFileBinaryContents, licenseFileContents),
                    iconFilename: iconFilename,
                    iconFileBinaryContents: iconFileBinaryContents,
                    readmeFilename: readmeFilename,
                    readmeFileContents: GetBinaryFileContents(readmeFileBinaryContents, readmeFileContents),
                    entryNames: entryNames);
            }

            private static byte[] GetBinaryFileContents(byte[] binaryContents, string stringContents)
            {
                if (binaryContents != null)
                {
                    return binaryContents;
                }

                if (stringContents != null)
                {
                    return Encoding.UTF8.GetBytes(stringContents);
                }

                return null;
            }
        }
    }
}