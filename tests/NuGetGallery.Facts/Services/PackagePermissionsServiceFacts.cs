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
        public class TheGetPermissionLevelMethod
        {
            private int _key = 0;

            [Flags]
            public enum ReturnsExpectedPermissionLevels_State
            {
                IsOwner = 1,
                IsSiteAdmin = 2,
                IsOrganizationAdmin = 4,
                IsOrganizationCollaborator = 8,
            }

            private static readonly IEnumerable<ReturnsExpectedPermissionLevels_State> _stateValues = 
                Enum.GetValues(typeof(ReturnsExpectedPermissionLevels_State)).Cast<ReturnsExpectedPermissionLevels_State>();

            public static IEnumerable<object[]> ReturnsExpectedPermissionLevels_Data
            {
                get
                {
                    for (int i = 0; i < Enum.GetValues(typeof(ReturnsExpectedPermissionLevels_State)).Cast<int>().Max() * 2; i++)
                    {
                        yield return new object[]
                        {
                            _stateValues.Where(s => Includes(i, s))
                        };
                    }
                }
            }

            private static bool Includes(int i, ReturnsExpectedPermissionLevels_State state)
            {
                return (i & (int)state) == 0;
            }

            [Theory]
            [MemberData(nameof(ReturnsExpectedPermissionLevels_Data))]
            public void ReturnsExpectedPermissionLevels(IEnumerable<ReturnsExpectedPermissionLevels_State> states)
            {
                // Arrange
                var expectedPermissionLevels = new List<PermissionLevel>() { PermissionLevel.Anonymous };

                var owners = new List<User>();

                var user = new User("testuser") { Key = _key++ };

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOwner))
                {
                    owners.Add(user);
                    expectedPermissionLevels.Add(PermissionLevel.Owner);
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsSiteAdmin))
                {
                    user.Roles.Add(new Role { Name = Constants.AdminRoleName });
                    expectedPermissionLevels.Add(PermissionLevel.SiteAdmin);
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOrganizationAdmin))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, true);
                    expectedPermissionLevels.Add(PermissionLevel.OrganizationAdmin);
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOrganizationCollaborator))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, false);
                    expectedPermissionLevels.Add(PermissionLevel.OrganizationCollaborator);
                }

                // Assert
                AssertPermissionLevels(owners, user, expectedPermissionLevels);
            }

            private void CreateOrganizationOwnerAndAddUserAsMember(List<User> owners, User user, bool isAdmin)
            {
                var organization = new Organization() { Memberships = new List<Membership>() };
                var organizationOwner = new User("testorganization") { Key = _key++, Organization = organization };
                owners.Add(organizationOwner);
                
                var organizationMembership = new Membership() { Organization = organization, Member = user, IsAdmin = isAdmin };
                organization.Memberships.Add(organizationMembership);
            }

            private void AssertPermissionLevels(IEnumerable<User> owners, User user, IEnumerable<PermissionLevel> expectedLevels)
            {
                AssertEqual(expectedLevels, PackagePermissionsService.GetPermissionLevels(owners, user));

                var principal = GetPrincipal(user);
                AssertEqual(expectedLevels, PackagePermissionsService.GetPermissionLevels(owners, principal));
            }

            private void AssertEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
            {
                Assert.True(!expected.Except(actual).Any());
                Assert.True(!actual.Except(expected).Any());
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
