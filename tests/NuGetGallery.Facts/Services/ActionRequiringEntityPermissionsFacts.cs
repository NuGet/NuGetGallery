// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Xunit;

namespace NuGetGallery
{
    public class ActionRequiringEntityPermissionsFacts
    {
        public class TheCheckPermissionsMethod
        {
            [Fact]
            public void ReturnsAccountPermissionsFailureWhenAccountCheckFails()
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.Unsatisfiable, (account, entity) => PermissionsCheckResult.Allowed);
                AssertIsAllowed(action, PermissionsCheckResult.AccountFailure);
            }

            [Fact]
            public void ReturnsPermissionsFailureThrownBySubclass()
            {
                var failureToReturn = (PermissionsCheckResult)99;
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (account, entity) => failureToReturn);
                AssertIsAllowed(action, failureToReturn);
            }
            
            private void AssertIsAllowed(IActionRequiringEntityPermissions<TestablePermissionsEntity> action, PermissionsCheckResult expectedFailure)
            {
                Assert.Equal(expectedFailure, action.CheckPermissions((User)null, null, null));
                Assert.Equal(expectedFailure, action.CheckPermissions((IPrincipal)null, null, null));
            }
        }

        public class TheCheckPermissionsOnBehalfOfAnyAccountMethod
        {
            [Fact]
            public void SuccessWithNullAccount()
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (account, entity) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(null, null));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(null, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new User[] { null }));
            }

            [Fact]
            public void FailureWithNullAccount()
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.Unsatisfiable, (a, e) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.AccountFailure, action.CheckPermissionsOnBehalfOfAnyAccount(null, null));
                Assert.Equal(PermissionsCheckResult.AccountFailure, action.CheckPermissionsOnBehalfOfAnyAccount(null, null, out var accountsAllowedOnBehalfOf));
                Assert.Empty(accountsAllowedOnBehalfOf);
            }

            [Fact]
            public void SuccessWithNullEntity()
            {
                var user = new User { Key = 1 };

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, null));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { user }));
            }

            [Fact]
            public void FailureWithNullEntity()
            {
                var failureResult = (PermissionsCheckResult)99;
                var user = new User { Key = 1 };

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => (PermissionsCheckResult)99);

                Assert.Equal(failureResult, action.CheckPermissionsOnBehalfOfAnyAccount(user, null));
                Assert.Equal(failureResult, action.CheckPermissionsOnBehalfOfAnyAccount(user, null, out var accountsAllowedOnBehalfOf));
                Assert.Empty(accountsAllowedOnBehalfOf);
            }

            [Fact]
            public void OrganizationsOfCurrentUserArePossibleAccounts()
            {
                CreateOrganizationWithUser(out var organization, out var user);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, null));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { user, organization }));
            }

            [Fact]
            public void OwnersOfEntityArePossibleAccounts()
            {
                CreateEntityWithOwner(out var entity, out var entityOwner);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(null, entity));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(null, entity, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { null, entityOwner }));
            }

            [Fact]
            public void OrganizationsOfCurrentUserAndOwnersOfEntityArePossibleAccounts()
            {
                CreateOrganizationWithUser(out var organization, out var user);
                CreateEntityWithOwner(out var entity, out var entityOwner);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, entity));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, entity, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { user, entityOwner, organization }));
            }

            public static IEnumerable<object[]> AccountsAllowedOnBehalfOfIsPopulatedWithExpectedAccounts_Data
            {
                get
                {
                    for (var i = 1; i < Math.Pow(2, 3); i++)
                    {
                        yield return new object[] { i };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(AccountsAllowedOnBehalfOfIsPopulatedWithExpectedAccounts_Data))]
            public void AccountsAllowedOnBehalfOfIsPopulatedWithExpectedAccounts(int expectedAccounts)
            {
                CreateOrganizationWithUser(out var organization, out var user);
                CreateEntityWithOwner(out var entity, out var entityOwner);

                var expectedAccountsList = new[] { user, entityOwner, organization }.Where(a => ((int)Math.Pow(2, a.Key - 1) & expectedAccounts) > 0);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, 
                    (a, e) => expectedAccountsList.Any(u => u.MatchesUser(a)) ? PermissionsCheckResult.Allowed : (PermissionsCheckResult)99);

                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, entity));
                Assert.Equal(PermissionsCheckResult.Allowed, action.CheckPermissionsOnBehalfOfAnyAccount(user, entity, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(expectedAccountsList));
            }

            private void CreateEntityWithOwner(out TestablePermissionsEntity entity, out User entityOwner)
            {
                entityOwner = new User { Key = 2 };
                entity = new TestablePermissionsEntity(new[] { entityOwner });
            }

            private void CreateOrganizationWithUser(out Organization organization, out User user)
            {
                organization = new Organization { Key = 3 };
                user = new User { Key = 1, Organizations = new Membership[] { new Membership() { Organization = organization } } };
            }
        }
    }
}
