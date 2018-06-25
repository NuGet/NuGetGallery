// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            // Assert
            Assert.Equal(SecurityPolicyAction.PackagePush, policyHandler.Action);
        }

        [Fact]
        public void Evaluate_ThrowsForNullArgument()
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>(() => policyHandler.Evaluate(null));
        }

        [Fact]
        public void Evaluate_SilentlySucceedsWhenMicrosoftUserDoesNotExist()
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();
            var fakes = new Fakes();
            var context = CreateTestContext(false, policyHandler.Policies, fakes.NewPackageVersion, null);

            // Act
            var result = policyHandler.Evaluate(context);

            // Assert
            Assert.Equal(SecurityPolicyResult.SuccessResult, result);
        }

        [Fact]
        public void Evaluate_CompliantPackage_CreatesWarningResultWhenPrefixReservationForNewIdIsMissing()
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var context = CreateTestContext(
                true,
                policyHandler.Policies,
                newMicrosoftCompliantPackage,
                null /* The new Package registration does not exist yet */);

            // Act
            var result = policyHandler.Evaluate(context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
            Assert.True(result.HasWarnings);
            Assert.NotEmpty(result.WarningMessages);
            Assert.Contains(Strings.SecurityPolicy_RequirePackagePrefixReserved, result.WarningMessages);
            Assert.False(newPackageRegistration.IsVerified);
        }

        [Fact]
        public void Evaluate_CompliantPackage_MarksPackageAsVerifiedWhenPrefixReservationByMicrosoftExists()
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "Prefix.NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var context = CreateTestContext(
                true,
                policyHandler.Policies,
                newMicrosoftCompliantPackage,
                null /* The new Package registration does not exist yet */);

            var microsoftUser = context.EntitiesContext.Users.Single(u => u.Username == RequireMicrosoftPackageCompliancePolicy.MicrosoftUsername);
            var reservedNamespace = new ReservedNamespace("Prefix.", isSharedNamespace: true, isPrefix: true);
            reservedNamespace.Owners.Add(microsoftUser);
            context.EntitiesContext.ReservedNamespaces.Add(reservedNamespace);

            // Act
            var result = policyHandler.Evaluate(context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.HasWarnings);
            Assert.Empty(result.WarningMessages);
            Assert.True(newPackageRegistration.IsVerified);
        }

        [Fact]
        public void Evaluate_CompliantPackage_AddsMicrosoftOwner()
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newMicrosoftCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var context = CreateTestContext(
                true,
                policyHandler.Policies,
                newMicrosoftCompliantPackage,
                null /* The new Package registration does not exist yet */);

            var microsoftUser = context.EntitiesContext.Users.Single(u => u.Username == RequireMicrosoftPackageCompliancePolicy.MicrosoftUsername);

            // Act
            var result = policyHandler.Evaluate(context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains(microsoftUser, newPackageRegistration.Owners);
        }

        [Theory]
        [MemberData(nameof(NonCompliantPackageMemberData))]
        public void Evaluate_NonCompliantPackage_CreatesErrorResult(Package nonCompliantPackage)
        {
            // Arrange
            var policyHandler = new RequireMicrosoftPackageCompliancePolicy();

            var nugetUser = new User("NuGet");
            var newPackageRegistration = new PackageRegistration { Id = "NewPackageId", Owners = new List<User> { nugetUser } };
            var newNonCompliantPackage = Fakes.CreateMicrosoftCompliantPackage("1.0", newPackageRegistration);

            var context = CreateTestContext(
                true,
                policyHandler.Policies,
                nonCompliantPackage,
                null /* The new Package registration does not exist yet */);

            // Act
            var result = policyHandler.Evaluate(context);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush, result.ErrorMessage);
            Assert.Null(newPackageRegistration.Owners.SingleOrDefault(u => u.Username == RequireMicrosoftPackageCompliancePolicy.MicrosoftUsername));
            Assert.False(newPackageRegistration.IsVerified);
        }

        private static PackageSecurityPolicyEvaluationContext CreateTestContext(
            bool microsoftUserExists,
            IEnumerable<UserSecurityPolicy> policies,
            Package package,
            PackageRegistration packageRegistration)
        {
            var entitiesContext = new FakeEntitiesContext();

            if (microsoftUserExists)
            {
                entitiesContext.Users.Add(
                    new User(RequireMicrosoftPackageCompliancePolicy.MicrosoftUsername));
            }

            var context = new PackageSecurityPolicyEvaluationContext(
                entitiesContext,
                policies,
                package,
                packageRegistration,
                It.IsAny<HttpContextBase>());

            return context;
        }

        private class Fakes
        {
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

                MicrosoftOrganization = new Organization(RequireMicrosoftPackageCompliancePolicy.MicrosoftUsername)
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
        }
    }
}
