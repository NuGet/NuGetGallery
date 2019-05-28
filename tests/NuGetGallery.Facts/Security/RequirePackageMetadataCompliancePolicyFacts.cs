// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Services.PackageManagement;
using NuGetGallery.Services.Security;
using NuGetGallery.Services.Telemetry;
using NuGetGallery.Services.UserManagement;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequirePackageMetadataCompliancePolicyFacts
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
            var newMicrosoftCompliantPackage = Fakes.CreateCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict);
            packageOwnershipManagementService
                .Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false /* commitChanges: false */))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var userService = new Mock<IUserService>(MockBehavior.Strict);
            userService
                .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, It.IsAny<bool>()))
                .Returns(Fakes.RequiredCoOwner)
                .Verifiable();

            var telemetryService = new Mock<ITelemetryService>().Object;

            var context = new PackageSecurityPolicyEvaluationContext(
                userService.Object,
                packageOwnershipManagementService.Object,
                telemetryService,
                subscription.Policies,
                newMicrosoftCompliantPackage,
                sourceAccount: nugetUser,
                targetAccount: nugetUser,
                httpContext: It.IsAny<HttpContextBase>());

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            packageOwnershipManagementService.VerifyAll();
            userService.VerifyAll();
        }

        [Fact]
        public async Task Evaluate_SilentlySucceedsWhenRequiredCoOwnerDoesNotExist()
        {
            // Arrange
            var nugetUser = new User("NuGet");
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();
            var fakes = new Fakes();
            var context = CreateTestContext(
                false,
                subscription.Policies,
                fakes.NewPackageVersion,
                packageRegistrationAlreadyExists: false,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

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
            var newMicrosoftCompliantPackage = Fakes.CreateCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
            packageOwnershipManagementService.Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false)).Returns(Task.CompletedTask);

            var context = CreateTestContext(
                true,
                subscription.Policies,
                newMicrosoftCompliantPackage,
                packageRegistrationAlreadyExists: false,
                packageOwnershipManagementService: packageOwnershipManagementService.Object,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
            Assert.True(result.HasWarnings);
            Assert.NotEmpty(result.WarningMessages);
            Assert.Contains(Strings.SecurityPolicy_RequirePackagePrefixReserved, result.WarningMessages);
            Assert.False(newPackageRegistration.IsVerified);
            packageOwnershipManagementService.Verify(s => s.AddPackageOwnerAsync(newPackageRegistration, Fakes.RequiredCoOwner, false), Times.Once);
        }

        [Fact]
        public async Task Evaluate_CompliantPackage_AddsRequiredCoOwner()
        {
            // Arrange
            var subscription = new MicrosoftTeamSubscription();
            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateCompliantPackage("1.0", newPackageRegistration);

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
            packageOwnershipManagementService.Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false)).Returns(Task.CompletedTask);

            var context = CreateTestContext(
                true,
                subscription.Policies,
                newMicrosoftCompliantPackage,
                packageRegistrationAlreadyExists: false,
                packageOwnershipManagementService: packageOwnershipManagementService.Object,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.True(result.Success);
            packageOwnershipManagementService.Verify(s => s.AddPackageOwnerAsync(newPackageRegistration, Fakes.RequiredCoOwner, false), Times.Once);
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

            var context = CreateTestContext(
                true,
                subscription.Policies,
                nonCompliantPackage,
                packageRegistrationAlreadyExists: false,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.False(result.Success);
            Assert.Null(newPackageRegistration.Owners.SingleOrDefault(u => u.Username == MicrosoftTeamSubscription.MicrosoftUsername));
            Assert.False(newPackageRegistration.IsVerified);
        }

        [Fact]
        public async Task Evaluate_NonCompliantPackageAuthor_CreatesErrorResult()
        {
            // Arrange
            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var packageAuthors = new[] { MicrosoftTeamSubscription.MicrosoftUsername, "The Not-Allowed Package Authors" };
            var nonCompliantPackage = Fakes.CreateCompliantPackage("1.0.0", newPackageRegistration, packageAuthors);

            var policy = RequirePackageMetadataCompliancePolicy.CreatePolicy(
                    MicrosoftTeamSubscription.Name,
                    MicrosoftTeamSubscription.MicrosoftUsername,
                    allowedCopyrightNotices: MicrosoftTeamSubscription.AllowedCopyrightNotices,
                    allowedAuthors: new[] { MicrosoftTeamSubscription.MicrosoftUsername },
                    isLicenseUrlRequired: true,
                    isProjectUrlRequired: true,
                    errorMessageFormat: Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush);

            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var context = CreateTestContext(
                true,
                new[] { policy },
                nonCompliantPackage,
                packageRegistrationAlreadyExists: false,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.False(result.Success);
            Assert.Null(newPackageRegistration.Owners.SingleOrDefault(u => u.Username == MicrosoftTeamSubscription.MicrosoftUsername));
            Assert.False(newPackageRegistration.IsVerified);
        }

        [Fact]
        public async Task Evaluate_DuplicatePackageAuthor_CreatesErrorResult()
        {
            // Arrange
            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var packageAuthors = new[] { MicrosoftTeamSubscription.MicrosoftUsername, MicrosoftTeamSubscription.MicrosoftUsername };
            var nonCompliantPackage = Fakes.CreateCompliantPackage("1.0.0", newPackageRegistration, packageAuthors);

            var policy = RequirePackageMetadataCompliancePolicy.CreatePolicy(
                    MicrosoftTeamSubscription.Name,
                    MicrosoftTeamSubscription.MicrosoftUsername,
                    allowedCopyrightNotices: MicrosoftTeamSubscription.AllowedCopyrightNotices,
                    allowedAuthors: new[] { MicrosoftTeamSubscription.MicrosoftUsername },
                    isLicenseUrlRequired: true,
                    isProjectUrlRequired: true,
                    errorMessageFormat: Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush);

            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var context = CreateTestContext(
                true,
                new[] { policy },
                nonCompliantPackage,
                packageRegistrationAlreadyExists: false,
                sourceAccount: nugetUser,
                targetAccount: nugetUser);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.False(result.Success);
            Assert.Null(newPackageRegistration.Owners.SingleOrDefault(u => u.Username == MicrosoftTeamSubscription.MicrosoftUsername));
            Assert.False(newPackageRegistration.IsVerified);
        }

        [Fact]
        public async Task Evaluate_CompliantPackageAuthors_CreatesSuccessResult()
        {
            // Arrange
            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var packageAuthors = new[] { MicrosoftTeamSubscription.MicrosoftUsername, "The Most-Awesome Package Authors" };
            var compliantPackage = Fakes.CreateCompliantPackage("1.0.0", newPackageRegistration, packageAuthors);

            var policy = RequirePackageMetadataCompliancePolicy.CreatePolicy(
                    MicrosoftTeamSubscription.Name,
                    MicrosoftTeamSubscription.MicrosoftUsername,
                    allowedCopyrightNotices: MicrosoftTeamSubscription.AllowedCopyrightNotices,
                    allowedAuthors: packageAuthors,
                    isLicenseUrlRequired: true,
                    isProjectUrlRequired: true,
                    errorMessageFormat: Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush);

            var policyHandler = new RequirePackageMetadataCompliancePolicy();

            var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
            packageOwnershipManagementService.Setup(m => m.AddPackageOwnerAsync(newPackageRegistration, It.IsAny<User>(), false)).Returns(Task.CompletedTask);

            var context = CreateTestContext(
                true,
                new[] { policy },
                compliantPackage,
                packageRegistrationAlreadyExists: false,
                sourceAccount: nugetUser,
                targetAccount: nugetUser,
                packageOwnershipManagementService: packageOwnershipManagementService.Object);

            // Act
            var result = await policyHandler.EvaluateAsync(context);

            // Assert
            Assert.True(result.Success);
            packageOwnershipManagementService.Verify(s => s.AddPackageOwnerAsync(newPackageRegistration, Fakes.RequiredCoOwner, false), Times.Once);
        }

        private static PackageSecurityPolicyEvaluationContext CreateTestContext(
            bool microsoftUserExists,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            bool packageRegistrationAlreadyExists,
            User sourceAccount,
            User targetAccount,
            IPackageOwnershipManagementService packageOwnershipManagementService = null,
            IReservedNamespaceService reservedNamespaceService = null)
        {
            var userService = new Mock<IUserService>(MockBehavior.Strict);
            if (microsoftUserExists)
            {
                userService
                    .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, false))
                    .Returns(Fakes.RequiredCoOwner);
            }
            else
            {
                userService
                    .Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, false))
                    .Returns((User)null);
            }

            var telemetryServiceMock = new Mock<ITelemetryService>();

            packageOwnershipManagementService = packageOwnershipManagementService ?? new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict).Object;

            var context = new PackageSecurityPolicyEvaluationContext(
                userService.Object,
                packageOwnershipManagementService,
                telemetryServiceMock.Object,
                policies,
                package,
                sourceAccount,
                targetAccount,
                It.IsAny<HttpContextBase>());

            return context;
        }

        private class Fakes
        {
            static Fakes()
            {
                RequiredCoOwner = new User(MicrosoftTeamSubscription.MicrosoftUsername);
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

            public static Package CreateCompliantPackage(string version, PackageRegistration packageRegistration, string[] allowedAuthors = null)
            {
                return new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration,
                    Copyright = "(c) Microsoft Corporation. All rights reserved.",
                    ProjectUrl = "https://github.com/NuGet/NuGetGallery",
                    LicenseUrl = "https://github.com/NuGet/NuGetGallery/blob/master/LICENSE.txt",
                    FlattenedAuthors = allowedAuthors == null ? "Microsoft" : string.Join(",", allowedAuthors)
                };
            }

            public static IReadOnlyCollection<Package> CreateNonCompliantPackages()
            {
                var nugetUser = new User("NuGet");
                var version = "1.0";
                var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };

                var nonCompliantPackages = new List<Package>();

                // Ensure copyright is non-compliant.
                var nonCompliantPackage1 = CreateCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage1.Copyright = null;
                nonCompliantPackages.Add(nonCompliantPackage1);

                // Ensure projectUrl is non-compliant.
                var nonCompliantPackage2 = CreateCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage2.ProjectUrl = null;
                nonCompliantPackages.Add(nonCompliantPackage2);

                // Ensure licenseUrl is non-compliant.
                var nonCompliantPackage3 = CreateCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage3.LicenseUrl = null;
                nonCompliantPackages.Add(nonCompliantPackage3);

                // Ensure authors is non-compliant.
                var nonCompliantPackage4 = CreateCompliantPackage(version, newPackageRegistration);
                nonCompliantPackage4.FlattenedAuthors = "Microsoft Communications Platform";
                nonCompliantPackages.Add(nonCompliantPackage4);

                return nonCompliantPackages;
            }

            public User Owner { get; }

            public Package NewPackageVersion { get; }

            public PackageRegistration ExistingPackageRegistration { get; }

            public static User RequiredCoOwner { get; }
        }
    }
}
