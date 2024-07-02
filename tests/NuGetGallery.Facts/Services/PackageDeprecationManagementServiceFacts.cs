// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeprecationManagementServiceFacts
    {
        public class TheGetPossibleAlternatePackageVersionsMethod : TestContainer
        {
            [Fact]
            public void ReturnsEmptyIfNoVersions()
            {
                // Arrange
                var id = "Crested.Gecko";
                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.None))
                    .Returns(Array.Empty<Package>());

                var controller = GetService<PackageDeprecationManagementService>();

                // Act
                var result = controller.GetPossibleAlternatePackageVersions(id);

                // Assert
                Assert.Empty(result);
            }

            [Fact]
            public void ReturnsAllAvailableVersionsInReverseVersionOrder()
            {
                // Arrange
                var id = "Crested.Gecko";
                var firstPackage = new Package
                {
                    Version = "1.0.0+build",
                    Listed = true
                };

                var secondPackage = new Package
                {
                    Version = "2.0.0+build",
                    Listed = true
                };

                var thirdPackage = new Package
                {
                    Version = "3.0.0+build",
                    Listed = true
                };

                var deletedPackage = new Package
                {
                    Version = "2.1.0-deleted",
                    PackageStatusKey = PackageStatus.Deleted,
                    Listed = true
                };

                var validatingPackage = new Package
                {
                    Version = "2.2.0-validating",
                    PackageStatusKey = PackageStatus.Validating,
                    Listed = true
                };

                var failedValidationPackage = new Package
                {
                    Version = "2.3.0-failedValidation",
                    PackageStatusKey = PackageStatus.FailedValidation,
                    Listed = true
                };

                var unlistedPackage = new Package
                {
                    Version = "2.4.0-unlisted",
                    Listed = false
                };

                var packages = new[]
                {
                    firstPackage,
                    deletedPackage,
                    validatingPackage,
                    failedValidationPackage,
                    thirdPackage,
                    secondPackage,
                    unlistedPackage
                };

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.None))
                    .Returns(packages);

                var controller = GetService<PackageDeprecationManagementService>();

                // Act
                var result = controller.GetPossibleAlternatePackageVersions(id);

                // Assert
                var expected = new[] { thirdPackage, secondPackage, firstPackage }
                    .Select(v => NuGetVersion.Parse(v.Version).ToNormalizedString())
                    .ToList();

                Assert.Equal(expected, result);
            }
        }

        public class TheDeprecateMethod : TestContainer
        {
            [Fact]
            public async Task ReturnsBadRequestIfOtherAndNoCustomMessage()
            {
                // Arrange
                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser: null,
                    id: "id", 
                    isOther: true);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(Strings.DeprecatePackage_CustomMessageRequired, result.Message);
            }

            public static IEnumerable<object[]> ReturnsBadRequestIfNoVersions_Data =
                MemberDataHelper.AsDataSet(null, Array.Empty<string>());

            [Theory]
            [MemberData(nameof(ReturnsBadRequestIfNoVersions_Data))]
            public async Task ReturnsBadRequestIfNoVersions(IReadOnlyCollection<string> versions)
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    "id", 
                    versions);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(Strings.DeprecatePackage_NoVersions, result.Message);
            }

            [Fact]
            public async Task ReturnsBadRequestIfLongCustomMessage()
            {
                // Arrange
                var currentUser = TestUtility.FakeUser;
                var service = GetService<PackageDeprecationManagementService>();

                var customMessage = new string('a', 1001);

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    "id",
                    new[] { "1.0.0" },
                    customMessage: customMessage);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_CustomMessageTooLong, 1000), result.Message);
            }

            public static IEnumerable<object[]> ReturnsNotFoundIfNoPackagesOrRegistrationMissing_Data
            {
                get
                {
                    var packageWithNullRegistration = new Package();
                    return MemberDataHelper.AsDataSet(Array.Empty<Package>(),
                        new[] { packageWithNullRegistration });
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsNotFoundIfNoPackagesOrRegistrationMissing_Data))]
            public async Task ReturnsNotFoundIfNoPackagesOrRegistrationMissing(IEnumerable<Package> packages)
            {
                // Arrange
                var id = "id";
                var currentUser = TestUtility.FakeUser;

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(packages.ToList())
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    "id", 
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_MissingRegistration, id), result.Message);

                packageService.Verify();
            }


            [Fact]
            public async Task ReturnsForbiddenIfFeatureFlagDisabled()
            {
                // Arrange
                var id = "id";
                var currentUser = TestUtility.FakeUser;

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(false)
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, result.Status);
                Assert.Equal(Strings.DeprecatePackage_Forbidden, result.Message);

                featureFlagService.Verify();
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task ReturnsForbiddenIfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, result.Status);
                Assert.Equal(Strings.DeprecatePackage_Forbidden, result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsForbiddenIfLocked(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id,
                    IsLocked = true
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.Forbidden, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_Locked, id), result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsBadRequestIfAlternatePackageRegistrationMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var alternatePackageId = "altId";
                packageService
                    .Setup(x => x.FindPackageRegistrationById(alternatePackageId))
                    .Returns((PackageRegistration)null)
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" }, 
                    alternatePackageId: alternatePackageId);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_NoAlternatePackageRegistration, alternatePackageId), result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsBadRequestIfAlternatePackageVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var alternatePackageId = "altId";
                var alternatePackageVersion = "1.2.3-alt";
                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion))
                    .Returns((Package)null)
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" }, 
                    alternatePackageId: alternatePackageId, 
                    alternatePackageVersion: alternatePackageVersion);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_NoAlternatePackage, alternatePackageId, alternatePackageVersion), result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id,
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_MissingVersion, id), result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsNotFoundIfSomeVersionMissing(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { "1.0.0" });

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, result.Status);
                Assert.Equal(string.Format(Strings.DeprecatePackage_MissingVersion, id), result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsBadRequestIfAlternateOfSelfById(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var version = "1.0.0";
                var package = new Package
                {
                    NormalizedVersion = version,
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                packageService
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration)
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id,
                    new[] { version },
                    alternatePackageId: id);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(Strings.DeprecatePackage_AlternateOfSelf, result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task ReturnsBadRequestIfAlternateOfSelfByVersion(User currentUser, User owner)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var version = "1.0.0";
                var package = new Package
                {
                    NormalizedVersion = version,
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package })
                    .Verifiable();

                packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(id, version))
                    .Returns(package)
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id, 
                    new[] { version }, 
                    alternatePackageId: id, 
                    alternatePackageVersion: version);

                // Assert
                Assert.Equal(HttpStatusCode.BadRequest, result.Status);
                Assert.Equal(Strings.DeprecatePackage_AlternateOfSelf, result.Message);

                featureFlagService.Verify();
                packageService.Verify();
            }

            public static IEnumerable<object[]> PackageDeprecationStates_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(false, false, false,
                        PackageDeprecationStatus.NotDeprecated);
                    yield return MemberDataHelper.AsData(false, false, true,
                        PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(false, true, false,
                        PackageDeprecationStatus.CriticalBugs);
                    yield return MemberDataHelper.AsData(true, false, false,
                        PackageDeprecationStatus.Legacy);
                    yield return MemberDataHelper.AsData(true, false, true,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, true, false,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.CriticalBugs);
                    yield return MemberDataHelper.AsData(false, true, true,
                        PackageDeprecationStatus.CriticalBugs | PackageDeprecationStatus.Other);
                    yield return MemberDataHelper.AsData(true, true, true,
                        PackageDeprecationStatus.Legacy | PackageDeprecationStatus.CriticalBugs | PackageDeprecationStatus.Other);
                }
            }

            public enum ReturnsSuccessful_AlternatePackage_State
            {
                None,
                RegistrationDifferentId,
                PackageSameId,
                PackageDifferentId
            }

            public static IEnumerable<object[]> ReturnsSuccessful_Data =
                MemberDataHelper.Combine(
                    Owner_Data,
                    PackageDeprecationStates_Data,
                    MemberDataHelper.EnumDataSet<ReturnsSuccessful_AlternatePackage_State>()).ToList();

            [Theory]
            [MemberData(nameof(ReturnsSuccessful_Data))]
            public Task ReturnsSuccessfulWithCustomMessage(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState)
            {
                return AssertSuccessful(
                    currentUser,
                    owner,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    expectedStatus,
                    alternatePackageState,
                    true);
            }

            /// <remarks>
            /// Deprecations where the only reason is "other" must have a custom message.
            /// </remarks>
            public static IEnumerable<object[]> ReturnsSuccessfulWithoutCustomMessage_Data =
                ReturnsSuccessful_Data.Where(x => !(bool)x[4]);

            [Theory]
            [MemberData(nameof(ReturnsSuccessfulWithoutCustomMessage_Data))]
            public Task ReturnsSuccessfulWithoutCustomMessage(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState)
            {
                return AssertSuccessful(
                    currentUser,
                    owner,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    expectedStatus,
                    alternatePackageState,
                    false);
            }

            private async Task AssertSuccessful(
                User currentUser,
                User owner,
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                PackageDeprecationStatus expectedStatus,
                ReturnsSuccessful_AlternatePackage_State alternatePackageState,
                bool hasCustomMessage)
            {
                // Arrange
                var id = "id";

                var registration = new PackageRegistration
                {
                    Id = id
                };

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationEnabled(currentUser, registration))
                    .Returns(true)
                    .Verifiable();

                registration.Owners.Add(owner);

                var package = new Package
                {
                    NormalizedVersion = "2.3.4",
                    PackageRegistration = registration
                };

                var package2 = new Package
                {
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = registration
                };

                var unselectedPackage = new Package
                {
                    NormalizedVersion = "1.3.2",
                    PackageRegistration = registration
                };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackagesById(id, PackageDeprecationFieldsToInclude.DeprecationAndRelationships))
                    .Returns(new[] { package, package2, unselectedPackage })
                    .Verifiable();

                string alternatePackageId = null;
                string alternatePackageVersion = null;
                PackageRegistration alternatePackageRegistration = null;
                Package alternatePackage = null;
                if (alternatePackageState != ReturnsSuccessful_AlternatePackage_State.None)
                {
                    var alternateRegistration = 
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.PackageSameId
                        ? registration
                        : new PackageRegistration
                        {
                            Id = "altId"
                        };

                    alternatePackageId = alternateRegistration.Id;

                    if (alternatePackageState == ReturnsSuccessful_AlternatePackage_State.RegistrationDifferentId)
                    {
                        alternatePackageRegistration = alternateRegistration;
                        packageService
                            .Setup(x => x.FindPackageRegistrationById(alternatePackageId))
                            .Returns(alternateRegistration)
                            .Verifiable();
                    }
                    else if (alternatePackageState == ReturnsSuccessful_AlternatePackage_State.PackageSameId || 
                        alternatePackageState == ReturnsSuccessful_AlternatePackage_State.PackageDifferentId)
                    {
                        alternatePackageVersion = "1.2.3-alt";
                        alternatePackage = new Package
                        {
                            NormalizedVersion = alternatePackageVersion,
                            PackageRegistration = alternateRegistration
                        };

                        packageService
                            .Setup(x => x.FindPackageByIdAndVersionStrict(alternatePackageId, alternatePackageVersion))
                            .Returns(alternatePackage)
                            .Verifiable();
                    }
                }

                var deprecationService = GetMock<IPackageDeprecationService>();

                var customMessage = hasCustomMessage ? "<message>" : null;

                deprecationService
                    .Setup(x => x.UpdateDeprecation(
                        new[] { package, package2 },
                        expectedStatus,
                        alternatePackageRegistration,
                        alternatePackage,
                        customMessage,
                        currentUser,
                        ListedVerb.Unchanged,
                        PackageDeprecatedVia.Web))
                    .Completes()
                    .Verifiable();

                var service = GetService<PackageDeprecationManagementService>();

                var packageNormalizedVersions = new[] { package.NormalizedVersion, package2.NormalizedVersion };

                // Act
                var result = await InvokeUpdateDeprecation(
                    service,
                    currentUser,
                    id,
                    packageNormalizedVersions,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    alternatePackageId,
                    alternatePackageVersion,
                    customMessage);

                // Assert
                Assert.Null(result);

                featureFlagService.Verify();
                packageService.Verify();
                deprecationService.Verify();
            }

            private static Task<UpdateDeprecationError> InvokeUpdateDeprecation(
                IPackageDeprecationManagementService deprecationManagementService,
                User currentUser,
                string id = null,
                IReadOnlyCollection<string> versions = null,
                bool isLegacy = false,
                bool hasCriticalBugs = false,
                bool isOther = false,
                string alternatePackageId = null,
                string alternatePackageVersion = null,
                string customMessage = null)
            {
                return deprecationManagementService.UpdateDeprecation(
                    currentUser,
                    id,
                    versions,
                    PackageDeprecatedVia.Web,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    alternatePackageId,
                    alternatePackageVersion,
                    customMessage);
            }
        }
    }
}