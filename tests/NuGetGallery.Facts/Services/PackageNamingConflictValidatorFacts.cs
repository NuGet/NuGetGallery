// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class PackageNamingConflictValidatorFacts
    {
        [Theory]
        [InlineData("Microsoft.FooBar", "Microsoft.FooBar", true)]
        [InlineData("Microsoft.FooBar", "microsoft.foobar", true)]
        [InlineData("Microsoft.FooBar", "Another.Package", false)]
        [InlineData("Microsoft.FooBar", "another.package", false)]
        [InlineData("Microsoft.FooBar", "Microsoft.FooBar contribution package", false)]
        private void TitleConflictsWithExistingRegistrationIdTests(string existingRegistrationId, string newPackageTitle, bool shouldBeConflict)
        {
            // Arrange
            var existingPackageRegistration = new PackageRegistration
            {
                Id = existingRegistrationId,
                Owners = new HashSet<User>()
            };

            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrationRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackageRegistration }.AsQueryable());

            var packageRepository = new Mock<IEntityRepository<Package>>();

            var target = new PackageNamingConflictValidator(packageRegistrationRepository.Object, packageRepository.Object);

            // Act
            var result = target.TitleConflictsWithExistingRegistrationId("NewPackageId", newPackageTitle);

            // Assert
            Assert.True(result == shouldBeConflict);
        }


        [Theory]
        [InlineData("ExistingPackageId", "ExistingPackageTitle", "NewPackageId", false)]
        [InlineData("ExistingPackageId", "ExistingPackageTitle", "newpackageid", false)]
        [InlineData("AnotherPackageTitle", "ExistingPackageTitle", "ExistingPackageTitle", true)]
        [InlineData("AnotherPackageTitle", "ExistingPackageTitle", "EXISTingPACKAGETiTLE", true)]
        public void IdConflictsWithExistingPackageTitleTests(string existingPackageId, string existingPackageTitle, string newPackageId, bool shouldBeConflict)
        {
            // Arrange
            var existingPackageRegistration = new PackageRegistration
            {
                Id = existingPackageId,
                Owners = new HashSet<User>()
            };
            var existingPackage = new Package
            {
                PackageRegistration = existingPackageRegistration,
                Version = "1.0.0",
                Title = existingPackageTitle
            };

            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrationRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackageRegistration }.AsQueryable());

            var packageRepository = new Mock<IEntityRepository<Package>>();
            packageRepository.Setup(r => r.GetAll()).Returns(new[] { existingPackage }.AsQueryable());

            var target = new PackageNamingConflictValidator(packageRegistrationRepository.Object, packageRepository.Object);

            // Act
            var result = target.IdConflictsWithExistingPackageTitle(newPackageId);

            // Assert
            Assert.True(result == shouldBeConflict);
        }
    }
}