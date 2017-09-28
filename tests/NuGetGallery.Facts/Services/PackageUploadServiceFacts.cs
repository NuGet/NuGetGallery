// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
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
            Mock<IValidationService> validationService = null)
        {
            if (packageService == null)
            {
                packageService = new Mock<IPackageService>();
            }

            packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
            packageService.Setup(x => x
                .CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                .Returns((PackageArchiveReader packageArchiveReader, PackageStreamMetadata packageStreamMetadata, User user, bool isVerified) =>
                {
                    var packageMetadata = PackageMetadata.FromNuspecReader(packageArchiveReader.GetNuspecReader());

                    var newPackage = new Package();
                    newPackage.PackageRegistration = new PackageRegistration { Id = packageMetadata.Id, IsVerified = isVerified };
                    newPackage.Version = packageMetadata.Version.ToString();
                    newPackage.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());

                    return Task.FromResult(newPackage);
                });

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();

                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService
                    .Setup(s => s.IsPushAllowed(It.IsAny<string>(), It.IsAny<User>(), out userOwnedMatchingNamespaces))
                    .Returns(true);
            }

            if (validationService == null)
            {
                validationService = new Mock<IValidationService>();
            }

            var packageUploadService = new Mock<PackageUploadService>(
                packageService.Object,
                new Mock<IPackageFileService>().Object,
                new Mock<IEntitiesContext>().Object,
                reservedNamespaceService.Object,
                validationService.Object);

            return packageUploadService.Object;
        }

        public class TheGeneratePackageAsyncMethod
        {
            [Fact]
            public async Task WillStartAsynchronousValidation()
            {
                var validationService = new Mock<IValidationService>();

                var id = "Microsoft.Aspnet.Mvc";
                var packageUploadService = CreateService(validationService: validationService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);
                var currentUser = new User();

                var package = await packageUploadService.GeneratePackageAsync(
                    id,
                    nugetPackage.Object,
                    new PackageStreamMetadata(),
                    currentUser);

                validationService.Verify(
                    x => x.StartValidationAsync(package),
                    Times.Once);
            }

            [Fact]
            public async Task WillCallCreatePackageAsyncCorrectly()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);

                var id = "Microsoft.Aspnet.Mvc";
                var packageUploadService = CreateService(packageService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);
                var currentUser = new User();

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), currentUser);

                packageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()), Times.Once);
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
                prefixes.ForEach(p => {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = matchingNamepsaces;
                reservedNamespaceService.Setup(s => s.IsPushAllowed(It.IsAny<string>(), It.IsAny<User>(), out userOwnedMatchingNamespaces))
                    .Returns(shouldMarkIdVerified);

                var packageUploadService = CreateService(reservedNamespaceService: reservedNamespaceService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), firstUser);

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
                prefixes.ForEach(p => {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.IsSharedNamespace = true;
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService.Setup(s => s.IsPushAllowed(It.IsAny<string>(), It.IsAny<User>(), out userOwnedMatchingNamespaces))
                    .Returns(true);

                var packageUploadService = CreateService(reservedNamespaceService: reservedNamespaceService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), lastUser);

                Assert.False(package.PackageRegistration.IsVerified);
            }
        }

        public class TheCommitPackageMethod : FactsBase
        {
            [Fact]
            public async Task SavesPackageToStorageAndDatabase()
            {
                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(_package, _packageFile),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
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
            public async Task DoesNotCommitToDatabaseWhenTheFileConflicts()
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
            public async Task DeletesPackageIfDatabaseCommitFails()
            {
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
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageFileService> _packageFileService;
            protected readonly Mock<IEntitiesContext> _entitiesContext;
            protected readonly Mock<IReservedNamespaceService> _reservedNamespaceService;
            protected readonly Mock<IValidationService> _validationService;
            protected Package _package;
            protected Stream _packageFile;
            protected ArgumentException _unexpectedException;
            protected InvalidOperationException _conflictException;
            protected readonly PackageUploadService _target;

            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _packageFileService = new Mock<IPackageFileService>();
                _entitiesContext = new Mock<IEntitiesContext>();
                _reservedNamespaceService = new Mock<IReservedNamespaceService>();
                _validationService = new Mock<IValidationService>();

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
                _conflictException = new InvalidOperationException("Conflict!");

                _target = new PackageUploadService(
                    _packageService.Object,
                    _packageFileService.Object,
                    _entitiesContext.Object,
                    _reservedNamespaceService.Object,
                    _validationService.Object);
            }
        }
    }
}