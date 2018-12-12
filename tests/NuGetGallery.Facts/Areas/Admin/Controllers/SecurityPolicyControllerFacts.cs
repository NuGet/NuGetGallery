// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Security;
using NuGetGallery.Areas.Admin.ViewModels;
using Xunit;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SecurityPolicyControllerFacts
    {
        [Fact]
        public void CtorThrowsIfEntitiesContextNull()
        {
            // Arrange.
            var policyService = new TestSecurityPolicyService();

            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new SecurityPolicyController(null, policyService));
        }

        [Fact]
        public void CtorThrowsIfPolicyServiceNull()
        {
            // Arrange.
            var entitiesContext = new Mock<IEntitiesContext>();

            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new SecurityPolicyController(entitiesContext.Object, null));
        }

        [Fact]
        public void IndexActionReturnsSubscriptionNames()
        {
            // Arrange.
            var policyService = new TestSecurityPolicyService();
            var entitiesContext = policyService.MockEntitiesContext.Object;
            var controller = new SecurityPolicyController(entitiesContext, policyService);

            // Act.
            var result = controller.Index();

            // Assert
            var model = ResultAssert.IsView<SecurityPolicyViewModel>(result);
            Assert.NotNull(model);
            Assert.NotNull(model.SubscriptionNames);
            Assert.Single(model.SubscriptionNames);
        }

        [Theory]
        [InlineData(null, 0, 0)]
        [InlineData("", 0, 0)]
        [InlineData("A, B, C", 3, 0)]
        [InlineData("D, E, F", 0, 3)]
        [InlineData("F,B, C,G,", 2, 2)]
        [InlineData("A\n B\n C", 3, 0)]
        [InlineData("D\n E\n F", 0, 3)]
        [InlineData("F\nB\n C\nG\n", 2, 2)]
        [InlineData("A\r B\r C", 3, 0)]
        [InlineData("D\r E\r F", 0, 3)]
        [InlineData("F\rB\r C\rG\r", 2, 2)]
        [InlineData("A\n\r B\n\r C", 3, 0)]
        [InlineData("D\n\r E\n\r F", 0, 3)]
        [InlineData("F\n\rB\n\r C\n\rG\n\r", 2, 2)]
        public void SearchFindsMatchingUsers(string query, int foundCount, int notFoundCount)
        {
            // Arrange.
            var policyService = new TestSecurityPolicyService();
            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(TestUsers.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);

            // Act.
            JsonResult result = controller.Search(query);

            // Assert
            dynamic data = result.Data;
            var users = data.Users as IEnumerable<UserSecurityPolicySubscriptions>;
            var usersNotFound = data.UsersNotFound as IEnumerable<string>;

            Assert.NotNull(users);
            Assert.Equal(foundCount, users.Count());
            Assert.Equal(notFoundCount, usersNotFound.Count());
        }

        [Fact]
        public async Task SearchReturnsUserSubscriptions()
        {
            // Arrange.
            var policyService = new TestSecurityPolicyService();
            var dbUsers = TestUsers.ToArray();
            var subscription = policyService.Mocks.UserPoliciesSubscription.Object;
            var subscriptionName = subscription.SubscriptionName;
            await policyService.SubscribeAsync(dbUsers[1], subscription);

            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(dbUsers.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);

            // Act.
            JsonResult result = controller.Search("A,B,C");

            // Assert.
            dynamic data = result.Data;
            var users = (data.Users as IEnumerable<UserSecurityPolicySubscriptions>)?.ToList();

            Assert.NotNull(users);
            Assert.Equal(3, users.Count());

            Assert.Equal(1, users[0].Subscriptions.Count);
            Assert.True(users[0].Subscriptions.ContainsKey(subscriptionName));
            Assert.False(users[0].Subscriptions[subscriptionName]);

            Assert.Equal(1, users[1].Subscriptions.Count);
            Assert.True(users[1].Subscriptions.ContainsKey(subscriptionName));
            Assert.True(users[1].Subscriptions[subscriptionName]);

            Assert.Equal(1, users[2].Subscriptions.Count);
            Assert.True(users[2].Subscriptions.ContainsKey(subscriptionName));
            Assert.False(users[2].Subscriptions[subscriptionName]);
        }

        [Fact]
        public async Task UpdateSubscribesUsers()
        {
            // Arrange.
            var users = TestUsers.ToList();
            var policyService = new TestSecurityPolicyService();
            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(users.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);
            var subscription = policyService.Mocks.UserPoliciesSubscription.Object;

            Assert.DoesNotContain(users, u => policyService.IsSubscribed(u, subscription));

            // Act.
            var model = new List<string>
            {
                $"{{\"u\":\"A\",\"g\":\"{subscription.SubscriptionName}\",\"v\":true}}",
                $"{{\"u\":\"B\",\"g\":\"{subscription.SubscriptionName}\",\"v\":false}}",
                $"{{\"u\":\"C\",\"g\":\"{subscription.SubscriptionName}\",\"v\":true}}"
            };
            var result = await controller.Update(model);

            // Assert.
            Assert.True(policyService.IsSubscribed(users[0], subscription));
            Assert.False(policyService.IsSubscribed(users[1], subscription));
            Assert.True(policyService.IsSubscribed(users[2], subscription));

            policyService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task UpdateUnsubscribesUsers()
        {
            // Arrange.
            var users = TestUsers.ToList();
            var policyService = new TestSecurityPolicyService();
            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(users.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);
            var subscription = policyService.Mocks.UserPoliciesSubscription.Object;

            users.ForEach(async u => await policyService.SubscribeAsync(u, subscription));
            policyService.MockEntitiesContext.ResetCalls();

            // Act.
            var model = new List<string>
            {
                $"{{\"u\":\"A\",\"g\":\"{subscription.SubscriptionName}\",\"v\":\"false\"}}",
                $"{{\"u\":\"B\",\"g\":\"{subscription.SubscriptionName}\",\"v\":\"true\"}}",
                $"{{\"u\":\"C\",\"g\":\"{subscription.SubscriptionName}\",\"v\":\"false\"}}"
            };
            var result = await controller.Update(model);

            // Assert.
            Assert.False(policyService.IsSubscribed(users[0], subscription));
            Assert.True(policyService.IsSubscribed(users[1], subscription));
            Assert.False(policyService.IsSubscribed(users[2], subscription));

            policyService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Exactly(2));
        }

        [Fact]
        public void IsSubscribedIgnoresPolicyState()
        {
            // Arrange.
            var users = TestUsers.ToList();
            var policyService = new TestSecurityPolicyService();
            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(users.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);
            var subscription = policyService.Mocks.UserPoliciesSubscription.Object;

            users.ForEach(async u => await policyService.SubscribeAsync(u, subscription));
            policyService.MockEntitiesContext.ResetCalls();

            // Act.
            // Simulates changes to the configurable state of all existing policy subscriptions
            users.ForEach(u => 
                u.SecurityPolicies.Where(p => p.Subscription == subscription.SubscriptionName).ToList().ForEach(p => 
                    p.Value = Guid.NewGuid().ToString()));

            // Assert.
            Assert.All(users, u => Assert.True(policyService.IsSubscribed(u, subscription)));
        }

        [Fact]
        public async Task UpdateIgnoresBadUsers()
        {
            // Arrange.
            var users = TestUsers.ToList();
            var policyService = new TestSecurityPolicyService();
            var entitiesMock = policyService.MockEntitiesContext;
            entitiesMock.Setup(c => c.Users).Returns(users.MockDbSet().Object);
            var controller = new SecurityPolicyController(entitiesMock.Object, policyService);
            var subscription = policyService.Mocks.UserPoliciesSubscription.Object;

            // Act.
            var model = new List<string>
            {
                $"{{\"u\":\"D\",\"g\":\"{subscription.SubscriptionName}\",\"v\":\"false\"}}"
            };
            var result = await controller.Update(model);

            // Assert.
            policyService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
        }

        private IEnumerable<User> TestUsers
        {
            get
            {
                yield return new User("A");
                yield return new User("B");
                yield return new User("C");
            }
        }
    }
}
