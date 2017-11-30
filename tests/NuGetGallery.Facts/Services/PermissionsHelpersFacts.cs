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
    public class PermissionsHelpersFacts
    {
        public class TheIsRequirementSatisfiedMethod
        {
            private int _key = 0;

            [Flags]
            public enum ReturnsSatisfiedRequirementWhenExpected_State
            {
                IsOwner = 1,
                IsSiteAdmin = 2,
                IsOrganizationAdmin = 4,
                IsOrganizationCollaborator = 8,
            }

            private static readonly IEnumerable<ReturnsSatisfiedRequirementWhenExpected_State> _stateValues = 
                Enum.GetValues(typeof(ReturnsSatisfiedRequirementWhenExpected_State)).Cast<ReturnsSatisfiedRequirementWhenExpected_State>();

            private static readonly int _maxStateValue =
                Enum.GetValues(typeof(ReturnsSatisfiedRequirementWhenExpected_State)).Cast<int>().Max();

            private static readonly int _maxPermissionsRequirement =
                Enum.GetValues(typeof(PermissionsRequirement)).Cast<int>().Max();

            public static IEnumerable<object[]> ReturnsSatisfiedRequirementWhenExpected_Data
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

            private static bool Includes(int i, ReturnsSatisfiedRequirementWhenExpected_State state)
            {
                return (i & (int)state) == 0;
            }

            [Theory]
            [MemberData(nameof(ReturnsSatisfiedRequirementWhenExpected_Data))]
            public void ReturnsSatisfiedRequirementWhenExpected(IEnumerable<ReturnsSatisfiedRequirementWhenExpected_State> states)
            {
                // Arrange
                var expectedPermissionLevel = PermissionsRequirement.Anonymous;

                var owners = new List<User>();

                var user = new User("testuser") { Key = _key++ };

                if (states.Contains(ReturnsSatisfiedRequirementWhenExpected_State.IsOwner))
                {
                    owners.Add(user);
                    expectedPermissionLevel |= PermissionsRequirement.Owner;
                }

                if (states.Contains(ReturnsSatisfiedRequirementWhenExpected_State.IsSiteAdmin))
                {
                    user.Roles.Add(new Role { Name = Constants.AdminRoleName });
                    expectedPermissionLevel |= PermissionsRequirement.SiteAdmin;
                }

                if (states.Contains(ReturnsSatisfiedRequirementWhenExpected_State.IsOrganizationAdmin))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, true);
                    expectedPermissionLevel |= PermissionsRequirement.OrganizationAdmin;
                }

                if (states.Contains(ReturnsSatisfiedRequirementWhenExpected_State.IsOrganizationCollaborator))
                {
                    CreateOrganizationOwnerAndAddUserAsMember(owners, user, false);
                    expectedPermissionLevel |= PermissionsRequirement.OrganizationCollaborator;
                }

                // Assert
                AssertIsRequirementSatisfied(owners, user, expectedPermissionLevel);
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

            private void AssertIsRequirementSatisfied(IEnumerable<User> owners, User user, PermissionsRequirement expectedSatisfiedPermissionsRequirement)
            {
                for (int i = 0; i < _maxPermissionsRequirement * 2; i++)
                {
                    var permissionsRequirement = (PermissionsRequirement)i;

                    var shouldSucceed = (permissionsRequirement & expectedSatisfiedPermissionsRequirement) > 0;

                    Assert.Equal(shouldSucceed, PermissionsHelpers.IsRequirementSatisfied(permissionsRequirement, user, owners));

                    var principal = GetPrincipal(user);
                    Assert.Equal(shouldSucceed, PermissionsHelpers.IsRequirementSatisfied(permissionsRequirement, principal, owners));
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
