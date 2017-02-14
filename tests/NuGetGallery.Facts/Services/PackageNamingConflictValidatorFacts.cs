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
        public void TitleConflictsWithExistingRegistrationIdTests(string existingRegistrationId, string newPackageTitle, bool shouldBeConflict)
        {
            // Arrange
            var existingPackageRegistration = new PackageRegistration
            {
                Id = existingRegistrationId,
                Owners = new HashSet<User>()
            };

            var target = CreateValidator(existingPackageRegistration, package: null);

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

            var target = CreateValidator(existingPackageRegistration, existingPackage);

            // Act
            var result = target.IdConflictsWithExistingPackageTitle(newPackageId);

            // Assert
            Assert.True(result == shouldBeConflict);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IdConflictsWithExistingPackageTitle_DoesNotSupportTitleReuseWithNonDeletedPackage(bool isExistingPackageListed)
        {
            // Arrange
            var packageRegistration = new PackageRegistration
            {
                Id = "A",
                Owners = new HashSet<User>()
            };
            var package = new Package
            {
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Title = "B",
                Listed = isExistingPackageListed,
                Deleted = false
            };
            var target = CreateValidator(packageRegistration, package);

            // Act
            var actualResult = target.IdConflictsWithExistingPackageTitle(registrationId: "B");

            // Assert
            Assert.True(actualResult);
        }

        [Fact]
        public void IdConflictsWithExistingPackageTitle_SupportsTitleReuseWithSoftDeletedPackage()
        {
            // Arrange
            var packageRegistration = new PackageRegistration
            {
                Id = "A",
                Owners = new HashSet<User>()
            };
            var package = new Package
            {
                PackageRegistration = packageRegistration,
                Version = "1.0.0",
                Title = "B",
                Listed = false,
                Deleted = true
            };
            var target = CreateValidator(packageRegistration, package);

            // Act
            var actualResult = target.IdConflictsWithExistingPackageTitle(registrationId: "B");

            // Assert
            Assert.False(actualResult);
        }

        private static PackageNamingConflictValidator CreateValidator(PackageRegistration packageRegistration, Package package)
        {
            var packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrationRepository.Setup(r => r.GetAll()).Returns(new[] { packageRegistration }.AsQueryable());

            var packageRepository = new Mock<IEntityRepository<Package>>();

            if (package != null)
            {
                packageRepository.Setup(r => r.GetAll()).Returns(new[] { package }.AsQueryable());
            }

            return new PackageNamingConflictValidator(packageRegistrationRepository.Object, packageRepository.Object);
        }
    }
}