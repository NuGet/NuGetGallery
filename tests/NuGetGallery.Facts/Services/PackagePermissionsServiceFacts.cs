// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class PackagePermissionsServiceFacts
    {
        public class TheGetPermissionsMethod
        {
            private IDictionary<PermissionLevel, IEnumerable<Permission>> _levelToPermission = 
                new Dictionary<PermissionLevel, IEnumerable<Permission>>()
                {
                    { PermissionLevel.None, new Permission[0] },
                    {
                        PermissionLevel.OrganizationCollaborator,
                        new []
                        {
                            Permission.DisplayMyPackage,
                            Permission.UploadNewVersion,
                            Permission.Edit,
                            Permission.Delete,
                        }
                    },
                    {
                        PermissionLevel.SiteAdmin,
                        new []
                        {
                            Permission.DisplayMyPackage,
                            Permission.UploadNewVersion,
                            Permission.Edit,
                            Permission.Delete,
                            Permission.ManagePackageOwners,
                        }
                    },
                    {
                        PermissionLevel.OrganizationAdmin,
                        new []
                        {
                            Permission.DisplayMyPackage,
                            Permission.UploadNewVersion,
                            Permission.Edit,
                            Permission.Delete,
                            Permission.ManagePackageOwners,
                            Permission.ReportMyPackage,
                        }
                    },
                    {
                        PermissionLevel.Owner,
                        new []
                        {
                            Permission.DisplayMyPackage,
                            Permission.UploadNewVersion,
                            Permission.Edit,
                            Permission.Delete,
                            Permission.ManagePackageOwners,
                            Permission.ReportMyPackage,
                        }
                    }
                };

            public static IEnumerable<object[]> GetPermissions_Data
            {
                get
                {
                    foreach (PermissionLevel permissionLevel in Enum.GetValues(typeof(PermissionLevel)))
                    {
                        yield return new object[]
                        {
                            permissionLevel
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(GetPermissions_Data))]
            public void ReturnsExpectedPermissions(PermissionLevel permissionLevel)
            {
                var actualPermissions = PackagePermissionsService.GetPermissions(permissionLevel);
                var expectedPermissions = _levelToPermission[permissionLevel];
                
                Assert.True(!expectedPermissions.Except(actualPermissions).Any());
                Assert.True(!actualPermissions.Except(expectedPermissions).Any());
            }
        }

        public class TheGetPermissionLevelMethod
        {
            [Fact]
            public void ReturnsExpectedPermissionLevel()
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

                var owners = new[] { owner, organizationOwner };

                // Assert
                // Co-owner
                AssertPermissionLevel(PermissionLevel.Owner, owners, owner);

                // Admin
                AssertPermissionLevel(PermissionLevel.SiteAdmin, owners, admin);

                // Organization
                AssertPermissionLevel(PermissionLevel.Owner, owners, organizationOwner);

                // Organization admin
                AssertPermissionLevel(PermissionLevel.OrganizationAdmin, owners, organizationAdmin);

                // Organization collaborator
                AssertPermissionLevel(PermissionLevel.OrganizationCollaborator, owners, organizationCollaborator);
            }

            private void AssertPermissionLevel(PermissionLevel expectedLevel, IEnumerable<User> owners, User user)
            {
                Assert.Equal(expectedLevel, PackagePermissionsService.GetPermissionLevel(owners, user));
                
                var principal = GetPrincipal(user);
                Assert.Equal(expectedLevel, PackagePermissionsService.GetPermissionLevel(owners, principal));
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
        }
    }
}
