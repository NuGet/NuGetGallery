// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequireMicrosoftPackageCompliancePolicyFacts
    {
        public static object[] NonCompliantPackageMemberData
        {
            get
            {
                return Fakes.CreateNonCompliantPackages().Select(p => new object[] { p }).ToArray();
            }
        }

        [Fact]
        public void Constructor_DefaultsToPackagePushSecurityPolicyAction()
        {
            // Arrange            
            // Act
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            // Assert
            Assert.Equal(SecurityPolicyAction.PackagePush, policyHandler.Action);
        }

        [Fact]
        public void Evaluate_ThrowsForNullArgument()
        {
            // Arrange
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            // Act
            // Assert
            Assert.ThrowsAsync<ArgumentNullException>(() => policyHandler.EvaluateAsync(null));
        }


        [Fact]
        public async Task Evaluate_DoesNotCommitChangesToEntityContext()
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict);
            packageOwnershipManagementService
                .Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false /* commitChanges: false */))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var userService = new Mock<IUserService>(MockBehavior.Strict);
            userService
                .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, It.IsAny<bool>()))
                .Returns(Fakes.MicrosoftUser)
                .Verifiable();


            var context = new PackageSecurityPolicyEvaluationContext(
                userService.Object, 
                packageOwnershipManagementService.Object, 
                subscription.Policies, 
                newMicrosoftCompliantPackage, 
                It.IsAny<HttpContextBase>());

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            packageOwnershipManagementService.VerifyAll();
            userService.VerifyAll();
        }

        [Fact]
        public async Task Evaluate_SilentlySucceedsWhenMicrosoftUserDoesNotExist()
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();
            var fakes = new Fakes();
            var context = CreateTestContext(false, subscription.Policies, fakes.NewPackageVersion, packageRegistrationAlreadyExists: false);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.Equal(SecurityPolicyResult.SuccessResult, result);
        }

        [Fact]
        public async Task Evaluate_CompliantPackage_CreatesWarningResultWhenPrefixReservationForNewIdIsMissing()
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
            packageOwnershipManagementService.Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false)).Returns(Task.CompletedTask);

            var context = CreateTestContext(
                true,
                subscription.Policies,
                newMicrosoftCompliantPackage,
                packageRegistrationAlreadyExists: false,
                packageOwnershipManagementService: packageOwnershipManagementService.Object);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
            Assert.True(result.HasWarnings);
            Assert.NotEmpty(result.WarningMessages);
            Assert.Contains(Strings.SecurityPolicy_RequirePackagePrefixReserved, result.WarningMessages);
            Assert.False(newPackageRegistration.IsVerified);
            packageOwnershipManagementService.Verify(s => s.AddPackageOwnerAsync(newPackageRegistration, Fakes.MicrosoftUser, false), Times.Once);
        }

        [Fact]
        public async Task Evaluate_CompliantPackage_AddsMicrosoftOwner()
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
            packageOwnershipManagementService.Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false)).Returns(Task.CompletedTask);

            var context = CreateTestContext(
                true,
                subscription.Policies,
                newMicrosoftCompliantPackage,
                packageRegistrationAlreadyExists: false,
                packageOwnershipManagementService: packageOwnershipManagementService.Object);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.True(result.Success);
            packageOwnershipManagementService.Verify(s => s.AddPackageOwnerAsync(newPackageRegistration, Fakes.MicrosoftUser, false), Times.Once);
        }

        [Theory]
        [MemberData(nameof(NonCompliantPackageMemberData))]
        public async Task Evaluate_NonCompliantPackage_CreatesErrorResult(Package nonCompliantPackage)
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newNonCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var context = CreateTestContext(
                true,
                subscription.Policies,
                nonCompliantPackage,
                packageRegistrationAlreadyExists: false);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.False(result.Success);
            Assert.Null(newPackageRegistration.Owners.SingleOrDefault(u => u.Username == MicrosoftTeamSubscription.MicrosoftUsername));
            Assert.False(newPackageRegistration.IsVerified);
        }

        private static PackageSecurityPolicyEvaluationContext CreateTestContext(
            bool microsoftUserExists,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            bool packageRegistrationAlreadyExists,
            IPackageOwnershipManagementService packageOwnershipManagementService = null,
            IReservedNamespaceService reservedNamespaceService = null)
        {
            var userService = new Mock<IUserService>(MockBehavior.Strict);
            if (microsoftUserExists)
            {
                userService
                    .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, false))
                    .Returns(Fakes.MicrosoftUser);
            }
            else
            {
                userService
                    .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, false))
                    .Returns((User)null);
            }

            packageOwnershipManagementService = packageOwnershipManagementService ?? new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict).Object;

            var context = new PackageSecurityPolicyEvaluationContext(
                userService.Object,
                packageOwnershipManagementService,
                policies,
                package,
                It.IsAny<HttpContextBase>());

            return context;
        }

        private class Fakes
        {
            static Fakes()
            {
                MicrosoftUser = new User(MicrosoftTeamSubscription.MicrosoftUsername);
            }

            public Fakes(
                string copyright = null,
                string projectUrl = null,
                string licenseUrl = null,
                string flattenedAuthors = null)
            {
                var key = 39;

                Owner = new User("testPackageOwner")
                {
                    Key = key++
                };

                MicrosoftOrganization = new Organization(MicrosoftTeamSubscription.MicrosoftUsername)
                {
                    Key = key++
                };

                ExistingPackageRegistration = new PackageRegistration
                {
                    Id = "FakePackage",
                    Owners = new List<User> { Owner },
                };

                NewPackageVersion = new Package
                {
                    Version = "1.0",
                    PackageRegistration = ExistingPackageRegistration,
                    Copyright = copyright,
                    ProjectUrl = projectUrl,
                    LicenseUrl = licenseUrl,
                    FlattenedAuthors = flattenedAuthors
                };

                ExistingPackageRegistration.Packages = new List<Package>
                {
                    NewPackageVersion,
                    new Package
                    {
                        Version = "2.0",
                        PackageRegistration = ExistingPackageRegistration,
                        Copyright = copyright,
                        ProjectUrl = projectUrl,
                        LicenseUrl = licenseUrl,
                        FlattenedAuthors = flattenedAuthors
                    }
                };
            }

            public static Package CreateMicrosoftCompliantPackage(string version, PackageRegistration packageRegistration)
            {
                return new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration,
                    Copyright = "(c) Microsoft Corporation. All rights reserved.",
                    ProjectUrl = "https://github.com/NuGet/NuGetGallery",
                    LicenseUrl = "https://github.com/NuGet/NuGetGallery/blob/master/LICENSE.txt",
                    FlattenedAuthors = "NuGet, Microsoft"
                };
            }

            public static IReadOnlyCollection<Package> CreateNonCompliantPackages()
            {
                var nugetUser = new User("NuGet");
                var version = "1.0";
                var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };

                var nonCompliantPackages = new List<Package>();

                // Ensure copyright is non-compliant.
                var nonCompliantPackage1 = CreateMicrosoftCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage1.Copyright = null;
                nonCompliantPackages.Add(nonCompliantPackage1);

                // Ensure projectUrl is non-compliant.
                var nonCompliantPackage2 = CreateMicrosoftCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage2.ProjectUrl = null;
                nonCompliantPackages.Add(nonCompliantPackage2);

                // Ensure licenseUrl is non-compliant.
                var nonCompliantPackage3 = CreateMicrosoftCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage3.LicenseUrl = null;
                nonCompliantPackages.Add(nonCompliantPackage3);

                // Ensure authors is non-compliant.
                var nonCompliantPackage4 = CreateMicrosoftCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage4.FlattenedAuthors = "NuGet";
                nonCompliantPackages.Add(nonCompliantPackage4);

                return nonCompliantPackages;
            }

            public User Owner { get; }

            public Package NewPackageVersion { get; }

            public Organization MicrosoftOrganization { get; }

            public PackageRegistration ExistingPackageRegistration { get; }
            public static User MicrosoftUser { get; }
        }
    }
}
