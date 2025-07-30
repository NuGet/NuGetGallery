// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers.FederatedCredentials;

public class FederatedCredentialsControllerFacts
{
    public class TheIndexMethod : FederatedCredentialsControllerFacts
    {
        [Fact]
        public void ReturnsView()
        {
            // Act
            var result = Target.Index(usernames: "mac\ncheese");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<ViewPoliciesViewModel>(viewResult.Model);
            Assert.Equal(["cheese", "mac"], model.Usernames);
            Assert.Equal("cheese", Assert.Single(model.UsernamesDoNotExist));
            var userPolicies = Assert.Single(model.UserPolices);
            Assert.Same(UserA, userPolicies.User);
            Assert.Equal(2, userPolicies.Policies.Count);
            Assert.Same(Policies[0], userPolicies.Policies[1]);
            Assert.Same(Policies[1], userPolicies.Policies[0]);
        }
    }

    public class TheDeletePolicyMethod : FederatedCredentialsControllerFacts
    {
        [Fact]
        public async Task DeletesPolicy()
        {
            // Act
            var result = await Target.DeletePolicy(policyKey: 4);

            // Assert
            Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.DeletePolicyAsync(Policies[0]), Times.Once);
        }
    }

    public class TheCreatePolicyMethod : FederatedCredentialsControllerFacts
    {
        [Fact]
        public async Task CreatedPolicy()
        {
            // Arrange
            var input = new AddPolicyViewModel
            {
                PolicyUser = "mac",
                PolicyPackageOwner = "mac-farm",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria =
                """
                {
                    "tid": "acf6141a-b108-45dd-bf31-9afaa7403463",
                    "oid": "07f3a0d5-1da7-4fee-ac38-6893653eba08"
                }
                """
            };

            FederatedCredentialService
                .Setup(x => x.AddEntraIdServicePrincipalPolicyAsync(
                    It.IsAny<User>(),
                    It.IsAny<User>(),
                    It.IsAny<EntraIdServicePrincipalCriteria>()))
                .ReturnsAsync(() => AddFederatedCredentialPolicyResult.Created(new FederatedCredentialPolicy { Key = 6 }));

            // Act
            var result = await Target.CreatePolicy(input);

            // Assert
            Assert.IsType<RedirectResult>(result);
            var invocation = Assert.Single(FederatedCredentialService.Invocations);
            Assert.Equal(nameof(FederatedCredentialService.Object.AddEntraIdServicePrincipalPolicyAsync), invocation.Method.Name);
            Assert.Same(UserA, invocation.Arguments[0]);
            Assert.Same(OrgA, invocation.Arguments[1]);
            var criteria = Assert.IsType<EntraIdServicePrincipalCriteria>(invocation.Arguments[2]);
            Assert.Equal("acf6141a-b108-45dd-bf31-9afaa7403463", criteria.TenantId.ToString());
            Assert.Equal("07f3a0d5-1da7-4fee-ac38-6893653eba08", criteria.ObjectId.ToString());
        }
    }

    public FederatedCredentialsControllerFacts()
    {
        UserRepository = new Mock<IEntityRepository<User>>();
        UserService = new Mock<IUserService>();
        FederatedCredentialService = new Mock<IFederatedCredentialService>();

        UserA = new User { Key = 2, Username = "mac" };
        OrgA = new Organization { Key = 3, Username = "mac-farm" };
        var baseTime = new DateTime(2024, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        Users = new List<User>
        {
            UserA,
            OrgA,
        };
        Policies = new List<FederatedCredentialPolicy>
        {
            new FederatedCredentialPolicy { Key = 4, Created = baseTime.AddHours(2), CreatedByUserKey = UserA.Key, CreatedBy = UserA, PackageOwnerUserKey = UserA.Key, PackageOwner = UserA },
            new FederatedCredentialPolicy { Key = 5, Created = baseTime.AddHours(1), CreatedByUserKey = UserA.Key, CreatedBy = UserA, PackageOwnerUserKey = OrgA.Key, PackageOwner = OrgA },
        };

        FederatedCredentialService
            .Setup(x => x.GetPoliciesRelatedToUserKeys(It.IsAny<IReadOnlyList<int>>()))
            .Returns(() => Policies);
        FederatedCredentialService
            .Setup(x => x.GetPolicyByKey(It.IsAny<int>()))
            .Returns<int>(k => Policies.FirstOrDefault(p => p.Key == k));
        UserRepository
            .Setup(x => x.GetAll())
            .Returns(() => new[] { UserA, OrgA }.AsQueryable());
        UserService
            .Setup(x => x.FindByUsername(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns<string, bool>((u, _) => Users.FirstOrDefault(x => x.Username == u));

        Target = new FederatedCredentialsController(
            UserRepository.Object,
            UserService.Object,
            FederatedCredentialService.Object);

        TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), Target);
    }

    public Mock<IEntityRepository<User>> UserRepository { get; }
    public Mock<IUserService> UserService { get; }
    public Mock<IFederatedCredentialService> FederatedCredentialService { get; }
    public User UserA { get; }
    public Organization OrgA { get; }
    public int CreatedByUserKey { get; }
    public List<User> Users { get; }
    public List<FederatedCredentialPolicy> Policies { get; }
    public FederatedCredentialsController Target { get; }
}
