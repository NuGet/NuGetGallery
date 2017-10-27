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
            private IDictionary<PackagePermissionsService.PermissionLevel, IEnumerable<PackagePermissionsService.Permission>> _levelToPermission = 
                new Dictionary<PackagePermissionsService.PermissionLevel, IEnumerable<PackagePermissionsService.Permission>>()
                {
                    { PackagePermissionsService.PermissionLevel.None, new PackagePermissionsService.Permission[0] },
                    {
                        PackagePermissionsService.PermissionLevel.OrganizationCollaborator,
                        new []
                        {
                            PackagePermissionsService.Permission.DisplayMyPackage,
                            PackagePermissionsService.Permission.Upload,
                            PackagePermissionsService.Permission.Edit,
                            PackagePermissionsService.Permission.Delete
                        }
                    },
                    {
                        PackagePermissionsService.PermissionLevel.SiteAdmin,
                        new []
                        {
                            PackagePermissionsService.Permission.DisplayMyPackage,
                            PackagePermissionsService.Permission.Upload,
                            PackagePermissionsService.Permission.Edit,
                            PackagePermissionsService.Permission.Delete,
                            PackagePermissionsService.Permission.ManagePackageOwners
                        }
                    },
                    {
                        PackagePermissionsService.PermissionLevel.OrganizationAdmin,
                        new []
                        {
                            PackagePermissionsService.Permission.DisplayMyPackage,
                            PackagePermissionsService.Permission.Upload,
                            PackagePermissionsService.Permission.Edit,
                            PackagePermissionsService.Permission.Delete,
                            PackagePermissionsService.Permission.ManagePackageOwners,
                            PackagePermissionsService.Permission.ReportMyPackage
                        }
                    },
                    {
                        PackagePermissionsService.PermissionLevel.Owner,
                        new []
                        {
                            PackagePermissionsService.Permission.DisplayMyPackage,
                            PackagePermissionsService.Permission.Upload,
                            PackagePermissionsService.Permission.Edit,
                            PackagePermissionsService.Permission.Delete,
                            PackagePermissionsService.Permission.ManagePackageOwners,
                            PackagePermissionsService.Permission.ReportMyPackage
                        }
                    }
                };

            public static IEnumerable<object[]> GetPermissions_Data
            {
                get
                {
                    foreach (PackagePermissionsService.PermissionLevel permissionLevel in Enum.GetValues(typeof(PackagePermissionsService.PermissionLevel)))
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
            public void ReturnsExpectedPermissions(PackagePermissionsService.PermissionLevel permissionLevel)
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
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.Owner, owners, owner);
                // Co-owner is owner
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.Owner, owners, owner);

                // Admin is owner if allowAdmin
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.SiteAdmin, owners, admin);

                // Organization is owner
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.Owner, owners, organizationOwner);

                // Organization admin is owner
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.OrganizationAdmin, owners, organizationAdmin);

                // Organization collaborator is owner if allowCollaborator
                AssertPermissionLevel(PackagePermissionsService.PermissionLevel.OrganizationCollaborator, owners, organizationCollaborator);
            }

            private void AssertPermissionLevel(PackagePermissionsService.PermissionLevel expectedLevel, IEnumerable<User> owners, User user)
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
