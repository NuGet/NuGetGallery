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
        public async Task WhenPolicyUserIsEmpty_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}"""
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            var model = Assert.IsType<ViewPoliciesViewModel>(viewResult.Model);
            Assert.Same(addPolicy, model.AddPolicy);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyUser"));
            Assert.Equal("The policy user field is required.", Target.ModelState["AddPolicy.PolicyUser"].Errors[0].ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task WhenPolicyUserIsNullOrWhitespace_AddsModelErrorAndReturnsView(string policyUser)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = policyUser,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}"""
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyUser"));
            Assert.Equal("The policy user field is required.", Target.ModelState["AddPolicy.PolicyUser"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenPolicyTypeIsNull_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyType = null,
                PolicyCriteria = """{"test": "value"}"""
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyType"));
            Assert.Equal("The policy type field is required.", Target.ModelState["AddPolicy.PolicyType"].Errors[0].ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WhenPolicyCriteriaIsNullOrWhitespace_AddsModelErrorAndReturnsView(string policyCriteria)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = policyCriteria
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyCriteria"));
            Assert.Equal("The policy criteria field is required.", Target.ModelState["AddPolicy.PolicyCriteria"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenMultipleFieldsInvalid_AddsAllModelErrors()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = null,
                PolicyType = null,
                PolicyCriteria = null
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyUser"));
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyType"));
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyCriteria"));
        }

        [Fact]
        public async Task WhenValidationPasses_CallsFederatedCredentialService()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"tenant":"test","object":"123"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = user,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    user,
                    "packageowner",
                    """{"tenant":"test","object":"123"}""",
                    "Test Policy",
                    FederatedCredentialType.EntraIdServicePrincipal))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("testuser", redirectResult.Url);
            Assert.Contains("Policy with key 42 added successfully", Target.TempData["MessageFortestuser"].ToString());

            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                user,
                "packageowner",
                """{"tenant":"test","object":"123"}""",
                "Test Policy",
                FederatedCredentialType.EntraIdServicePrincipal), Times.Once);
        }

        [Fact]
        public async Task WhenServiceReturnsBadRequest_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "Invalid criteria format",
                nameof(FederatedCredentialPolicy.Criteria));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FederatedCredentialType>()))
                .ReturnsAsync(badRequestResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyCriteria"));
            Assert.Equal("Invalid criteria format", Target.ModelState["AddPolicy.PolicyCriteria"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenServiceReturnsUnauthorized_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var unauthorizedResult = FederatedCredentialPolicyValidationResult.Unauthorized(
                "User does not have permissions",
                nameof(FederatedCredentialPolicy.PackageOwner));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FederatedCredentialType>()))
                .ReturnsAsync(unauthorizedResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyPackageOwner"));
            Assert.Equal("User does not have permissions", Target.ModelState["AddPolicy.PolicyPackageOwner"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenValidPolicyWithNullPackageOwner_PassesNullToService()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = null, // Null package owner
                PolicyType = FederatedCredentialType.GitHubActions,
                PolicyCriteria = """{"owner":"test","repo":"test"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = user,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    user,
                    null,
                    """{"owner":"test","repo":"test"}""",
                    "Test Policy",
                    FederatedCredentialType.GitHubActions))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                user,
                null,
                """{"owner":"test","repo":"test"}""",
                "Test Policy",
                FederatedCredentialType.GitHubActions), Times.Once);
        }

        [Fact]
        public async Task WhenValidPolicyWithNullPolicyName_PassesNullToService()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.GitHubActions,
                PolicyCriteria = """{"owner":"test","repo":"test"}""",
                PolicyName = null // Null policy name
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = user,
                    PolicyName = null
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    user,
                    "packageowner",
                    """{"owner":"test","repo":"test"}""",
                    null,
                    FederatedCredentialType.GitHubActions))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                user,
                "packageowner",
                """{"owner":"test","repo":"test"}""",
                null,
                FederatedCredentialType.GitHubActions), Times.Once);
        }

        [Theory]
        [InlineData(FederatedCredentialType.EntraIdServicePrincipal)]
        [InlineData(FederatedCredentialType.GitHubActions)]
        public async Task WhenDifferentPolicyTypes_PassesCorrectTypeToService(FederatedCredentialType policyType)
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = policyType,
                PolicyCriteria = """{"test": "value"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = user,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    user,
                    "packageowner",
                    """{"test": "value"}""",
                    "Test Policy",
                    policyType))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                user,
                "packageowner",
                """{"test": "value"}""",
                "Test Policy",
                policyType), Times.Once);
        }

        [Fact]
        public async Task WhenModelErrorMappingForDifferentProperties_MapsCorrectly()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "Policy name too long",
                nameof(FederatedCredentialPolicy.PolicyName));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FederatedCredentialType>()))
                .ReturnsAsync(badRequestResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyName"));
            Assert.Equal("Policy name too long", Target.ModelState["AddPolicy.PolicyName"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenServiceErrorWithUnknownPropertyName_MapsToGeneralAddPolicyError()
        {
            // Arrange
            var user = new User { Key = 10, Username = "testuser" };
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "testuser",
                PolicyPackageOwner = "packageowner",
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyName = "Test Policy"
            };

            UserService.Setup(x => x.FindByUsername("testuser", false)).Returns(user);

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "General error",
                "UnknownProperty");

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FederatedCredentialType>()))
                .ReturnsAsync(badRequestResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy"));
            Assert.Equal("General error", Target.ModelState["AddPolicy"].Errors[0].ErrorMessage);
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
