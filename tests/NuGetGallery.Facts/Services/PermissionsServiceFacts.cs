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
    public class PermissionsServiceFacts
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

            private static readonly int _maxStateValue =
                Enum.GetValues(typeof(ReturnsExpectedPermissionLevels_State)).Cast<int>().Max();

            private static readonly int _maxPermissionLevel =
                Enum.GetValues(typeof(PermissionLevel)).Cast<int>().Max();

            public static IEnumerable<object[]> ReturnsExpectedPermissionLevels_Data
            {
                get
                {
                    for (int i = 0; i < _maxStateValue * 2; i++)
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
                var expectedPermissionLevel = PermissionLevel.Anonymous;

                var owners = new List<User>();

                var user = new User("testuser") { Key = _key++ };

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOwner))
                {
                    owners.Add(user);
                    expectedPermissionLevel |= PermissionLevel.Owner;
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsSiteAdmin))
                {
                    user.Roles.Add(new Role { Name = Constants.AdminRoleName });
                    expectedPermissionLevel |= PermissionLevel.SiteAdmin;
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOrganizationAdmin))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, true);
                    expectedPermissionLevel |= PermissionLevel.OrganizationAdmin;
                }

                if (states.Contains(ReturnsExpectedPermissionLevels_State.IsOrganizationCollaborator))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, false);
                    expectedPermissionLevel |= PermissionLevel.OrganizationCollaborator;
                }

                // Assert
                AssertPermissionLevels(owners, user, expectedPermissionLevel);
            }

            private void CreateOrganizationOwnerAndAddUserAsMember(List<User> owners, User user, bool isAdmin)
            {
                var organization = new Organization();
                organization.Members = new[]
                {
                    new Membership()
                    {
                        Organization = organization,
                        Member = user,
                        IsAdmin = isAdmin
                    }
                };
                owners.Add(organization);
            }

            private void AssertPermissionLevels(IEnumerable<User> owners, User user, PermissionLevel expectedLevel)
            {
                for (int i = 0; i < _maxPermissionLevel * 2; i++)
                {
                    var permissionLevel = (PermissionLevel)i;

                    var shouldSucceed = (permissionLevel & expectedLevel) > 0;

                    Assert.Equal(shouldSucceed, PermissionsService.IsActionAllowed(owners, user, permissionLevel));

                    var principal = GetPrincipal(user);
                    Assert.Equal(shouldSucceed, PermissionsService.IsActionAllowed(owners, principal, permissionLevel));
                }
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
