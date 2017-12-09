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
        private class Entity
        {
            public IEnumerable<User> Owners { get; }

            public Entity(IEnumerable<User> owners)
            {
                Owners = owners;
            }
        }

        private class TestableActionRequiringEntityPermissions 
            : ActionRequiringEntityPermissions<Entity>
        {
            private Func<User, Entity, PermissionsCheckResult> _isAllowedOnEntity;

            public TestableActionRequiringEntityPermissions(PermissionsRequirement accountOnBehalfOfPermissionsRequirement, Func<User, Entity, PermissionsCheckResult> isAllowedOnEntity) 
                : base(accountOnBehalfOfPermissionsRequirement)
            {
                _isAllowedOnEntity = isAllowedOnEntity;
            }

            protected override IEnumerable<User> GetOwners(Entity entity)
            {
                return entity != null ? entity.Owners : Enumerable.Empty<User>();
            }

            protected override PermissionsCheckResult IsAllowedOnEntity(User account, Entity entity)
            {
                return _isAllowedOnEntity(account, entity);
            }
        }

        public class TheIsAllowedMethod
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
                var failureToReturn = PermissionsCheckResult.UnknownFailure;
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (account, entity) => failureToReturn);
                AssertIsAllowed(action, failureToReturn);
            }
            
            private void AssertIsAllowed(ActionRequiringEntityPermissions<Entity> action, PermissionsCheckResult expectedFailure)
            {
                Assert.Equal(expectedFailure, action.IsAllowed((User)null, null, null));
                Assert.Equal(expectedFailure, action.IsAllowed((IPrincipal)null, null, null));
            }
        }

        public class TheTryGetAccountsIsAllowedOnBehalfOfMethod
        {
            [Fact]
            public void SuccessWithNullAccount()
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (account, entity) => PermissionsCheckResult.Allowed);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(null, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new User[] { null }));
            }

            [Fact]
            public void FailureWithNullAccount()
            {
                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.Unsatisfiable, (a, e) => PermissionsCheckResult.Allowed);

                Assert.False(action.TryGetAccountsIsAllowedOnBehalfOf(null, null, out var accountsAllowedOnBehalfOf));
                Assert.Empty(accountsAllowedOnBehalfOf);
            }

            [Fact]
            public void SuccessWithNullEntity()
            {
                var user = new User { Key = 1 };

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(user, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { user }));
            }

            [Fact]
            public void FailureWithNullEntity()
            {
                var user = new User { Key = 1 };

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.UnknownFailure);

                Assert.False(action.TryGetAccountsIsAllowedOnBehalfOf(user, null, out var accountsAllowedOnBehalfOf));
                Assert.Empty(accountsAllowedOnBehalfOf);
            }

            [Fact]
            public void OrganizationsOfCurrentUserArePossibleAccounts()
            {
                CreateOrganizationWithUser(out var organization, out var user);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(user, null, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { user, organization }));
            }

            [Fact]
            public void OwnersOfEntityArePossibleAccounts()
            {
                CreateEntityWithOwner(out var entity, out var entityOwner);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(null, entity, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(new[] { null, entityOwner }));
            }

            [Fact]
            public void OrganizationsOfCurrentUserAndOwnersOfEntityArePossibleAccounts()
            {
                CreateOrganizationWithUser(out var organization, out var user);
                CreateEntityWithOwner(out var entity, out var entityOwner);

                var action = new TestableActionRequiringEntityPermissions(PermissionsRequirement.None, (a, e) => PermissionsCheckResult.Allowed);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(user, entity, out var accountsAllowedOnBehalfOf));
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
                    (a, e) => expectedAccountsList.Any(u => u.MatchesUser(a)) ? PermissionsCheckResult.Allowed : PermissionsCheckResult.UnknownFailure);

                Assert.True(action.TryGetAccountsIsAllowedOnBehalfOf(user, entity, out var accountsAllowedOnBehalfOf));
                Assert.True(accountsAllowedOnBehalfOf.SequenceEqual(expectedAccountsList));
            }

            private void CreateEntityWithOwner(out Entity entity, out User entityOwner)
            {
                entityOwner = new User { Key = 2 };
                entity = new Entity(new[] { entityOwner });
            }

            private void CreateOrganizationWithUser(out Organization organization, out User user)
            {
                organization = new Organization { Key = 3 };
                user = new User { Key = 1, Organizations = new Membership[] { new Membership() { Organization = organization } } };
            }
        }
    }
}
