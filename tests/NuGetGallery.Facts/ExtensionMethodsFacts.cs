// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Moq;
using NuGet.Frameworks;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class ExtensionMethodsFacts
    {
        public class TheIsOwnerMethod
        {
            [Flags]
            private enum IsOwner_Data_States
            {
                AllowAdmin = 1,
                AllowCollaborator = 2
            }

            public static IEnumerable<object[]> IsOwner_Data
            {
                get
                {
                    for (int i = 0; i < (int)Enum.GetValues(typeof(IsOwner_Data_States)).Cast<int>().Max() * 2; i++)
                    {
                        var allowAdmin = (i & (int)IsOwner_Data_States.AllowAdmin) == 0;
                        var allowCollaborator = (i & (int)IsOwner_Data_States.AllowCollaborator) == 0;

                        yield return new object[] { allowAdmin, allowCollaborator };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(IsOwner_Data))]
            public void IsOwner(bool allowAdmin, bool allowCollaborator)
            {
                // Arrange
                var key = 0;

                var owner = new User("testuser") { Key = key++ };

                var admin = new User("testadmin") { Key = key++ };
                admin.Roles.Add(new Role { Name = Constants.AdminRoleName });

                var organization = new Organization() { Memberships = new List<Membership>() };
                var organizationOwner = new User("testorganization") { Key = key++, Organization = organization };

                var organizationAdmin = new User("testorganizationadmin") { Key = key++ };
                var organizationAdminMembership = new Membership() { Organization = organization, Member = organizationAdmin, IsAdmin = true };
                organization.Memberships.Add(organizationAdminMembership);

                var organizationCollaborator = new User("testorganizationcollaborator") { Key = key++ };
                var organizationCollaboratorMembership = new Membership() { Organization = organization, Member = organizationCollaborator, IsAdmin = false };
                organization.Memberships.Add(organizationCollaboratorMembership);

                var organizationCollaboratorAdmin = new User("testorganizationcollaboratoradmin") { Key = key++ };
                organizationCollaboratorAdmin.Roles.Add(new Role { Name = Constants.AdminRoleName });
                var organizationCollaboratorAdminMembership = new Membership() { Organization = organization, Member = organizationCollaboratorAdmin, IsAdmin = false };
                organization.Memberships.Add(organizationCollaboratorAdminMembership);

                var packageRegistration = new PackageRegistration() { Owners = new[] { owner, organizationOwner } };
                var package = new Package() { PackageRegistration = packageRegistration };

                // Assert
                // Co-owner is owner
                AssertIsOwner(owner, package, packageRegistration, allowAdmin, allowCollaborator, true);

                // Admin is owner if allowAdmin
                AssertIsOwner(admin, package, packageRegistration, allowAdmin, allowCollaborator, allowAdmin);

                // Organization is owner
                AssertIsOwner(organizationOwner, package, packageRegistration, allowAdmin, allowCollaborator, true);

                // Organization admin is owner
                AssertIsOwner(organizationAdmin, package, packageRegistration, allowAdmin, allowCollaborator, true);

                // Organization collaborator is owner if allowCollaborator
                AssertIsOwner(organizationCollaborator, package, packageRegistration, allowAdmin, allowCollaborator, allowCollaborator);

                // Admin and organization collaborator is owner if either allowAdmin or allowCollaborator
                AssertIsOwner(organizationCollaboratorAdmin, package, packageRegistration, allowAdmin, allowCollaborator, allowAdmin || allowCollaborator);
            }

            private IPrincipal GetPrincipal(User u)
            {
                var identityMock = new Mock<IIdentity>();
                identityMock.Setup(x => x.Name).Returns(u.Username);
                identityMock.Setup(x => x.IsAuthenticated).Returns(true);

                var principalMock = new Mock<IPrincipal>();
                principalMock.Setup(x => x.Identity).Returns(identityMock.Object);
                principalMock.Setup(x => x.IsInRole(It.IsAny<string>())).Returns<string>(role => u.IsInRole(role));
                return principalMock.Object;
            }

            private void AssertIsOwner(User user, Package package, PackageRegistration packageRegistration, bool allowAdmin, bool allowCollaborator, bool shouldSucceed)
            {
                Assert.Equal(shouldSucceed, packageRegistration.IsOwner(user, allowAdmin, allowCollaborator));
                Assert.Equal(shouldSucceed, package.IsOwner(user, allowAdmin, allowCollaborator));

                var principal = GetPrincipal(user);
                Assert.Equal(shouldSucceed, packageRegistration.IsOwner(principal, allowAdmin, allowCollaborator));
                Assert.Equal(shouldSucceed, package.IsOwner(principal, allowAdmin, allowCollaborator));
            }
        }

        public class TheGetCurrentApiKeyCredentialMethod
        {
            [Theory]
            [InlineData("apikey.v2")]
            [InlineData("apikey.verify.v1")]
            public void ReturnsApiKeyMatchingClaim(string credentialType)
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(credentialType, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, "B"));

                // Act
                var credential = user.GetCurrentApiKeyCredential(identity);

                // Assert
                Assert.Equal("B", credential.Value);
            }

            [Fact]
            public void ReturnsNullIfNoApiKeyClaim()
            {
                // Arrange
                var user = new User("testuser");
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "A"));
                user.Credentials.Add(new Credential(CredentialTypes.ApiKey.V2, "B"));

                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.LocalUser);
                
                // Act & Assert
                Assert.Null(user.GetCurrentApiKeyCredential(identity));
            }
        }

        public class TheHasPackageVerifyScopeClaimMethod
        {
            [Fact]
            public void ReturnsTrueIfPackageVerifyScopeClaimExists()
            {
                // Arrange
                var scope = "[{\"a\":\"package:verify\", \"s\":\"foo\"}]";
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scope));

                // Act & Assert
                Assert.True(identity.HasPackageVerifyScopeClaim());
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:unlist\", \"s\":\"foo\"}]")]
            public void ReturnsFalseIfPackageVerifyScopeClaimDoesNotExist(string scope)
            {
                // Arrange
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty),
                    new Claim(NuGetClaims.Scope, scope));

                // Act & Assert
                Assert.False(identity.HasPackageVerifyScopeClaim());
            }

            [Fact]
            public void ReturnsFalseIfScopeClaimDoesNotExist()
            {
                // Arrange
                var identity = AuthenticationService.CreateIdentity(
                    new User("testuser"),
                    AuthenticationTypes.ApiKey,
                    new Claim(NuGetClaims.ApiKey, string.Empty));

                // Act & Assert
                Assert.False(identity.HasPackageVerifyScopeClaim());
            }
        }

        public class TheToFriendlyNameMethod
        {
            [Theory]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Silverlight 4.0, Windows 8.0, WindowsPhone 7.1)", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable-net45+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Windows 8.0)", "portable40-net40+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable45-net45+win8")]
            public void CorrectlyConvertsShortNameToFriendlyName(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName();
                Assert.Equal(expected, actual);
            }

            [Theory]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library", "portable-net45+win8")]
            [InlineData("Portable Class Library", "portable40-net45+win8")]
            [InlineData("Portable Class Library", "portable45-net45+win8")]
            public void DoesNotRecurseWhenAllowRecurseProfileFalse(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName(allowRecurseProfile: false);
                Assert.Equal(expected, actual);
            }
        }
    }
}
