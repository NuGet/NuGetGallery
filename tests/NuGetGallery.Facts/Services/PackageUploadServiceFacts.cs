// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    }
}