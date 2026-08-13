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
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;
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
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WhenPolicyUserIsNullOrEmptyOrWhitespace_AddsModelErrorAndReturnsView(string policyUser)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = policyUser
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
        public async Task WhenPolicyUserDoesNotExist_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = "anyuser"
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyUser"));
            Assert.Equal("The policy user 'anyuser' does not exist.", Target.ModelState["AddPolicy.PolicyUser"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenPolicyTypeIsNull_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyType = null
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
        public async Task WhenPolicyCriteriaIsNullOrEmptyOrWhitespace_AddsModelErrorAndReturnsView(string policyCriteria)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(", ")]
        [InlineData("AnyScope1, ")]
        [InlineData("AnyScope1, AnyScope2 ,  ")]
        public async Task WhenPolicyScopesIsInvalid_AddsModelErrorAndReturnsView(string policyScopes)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyScopes = policyScopes
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyScopes"));
            Assert.Equal("The policy scopes require at least one valid allowed action.", Target.ModelState["AddPolicy.PolicyScopes"].Errors[0].ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" \n ")]
        [InlineData(" \r\n ")]
        [InlineData(" \r\n <policySubject1> ")]
        public async Task WhenPolicySubjectsIsInvalid_AddsModelErrorAndReturnsView(string policySubjects)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicySubjects = policySubjects
            };

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(Target.ModelState.IsValid);
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicySubjects"));
            Assert.Equal("The policy scopes require at least one valid glob pattern or package.", Target.ModelState["AddPolicy.PolicySubjects"].Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task WhenMultipleFieldsInvalid_AddsAllModelErrors()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyUser = null,
                PolicyType = null,
                PolicyCriteria = null,
                PolicyScopes = null,
                PolicySubjects = null,
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
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicyScopes"));
            Assert.True(Target.ModelState.ContainsKey("AddPolicy.PolicySubjects"));
        }

        [Fact]
        public async Task WhenValidationPasses_CallsFederatedCredentialService()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"tenant":"test","object":"123"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = UserA,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    UserA,
                    OrgA.Username,
                    """{"tenant":"test","object":"123"}""",
                    "Test Policy",
                    FederatedCredentialType.EntraIdServicePrincipal,
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains(UserA.Username, redirectResult.Url);
            Assert.Contains("Policy with key 42 added successfully", Target.TempData[$"MessageFor{UserA.Username}"].ToString());

            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                UserA,
                OrgA.Username,
                """{"tenant":"test","object":"123"}""",
                "Test Policy",
                FederatedCredentialType.EntraIdServicePrincipal,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()), Times.Once);
        }

        [Fact]
        public async Task WhenServiceReturnsBadRequest_AddsModelErrorAndReturnsView()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "Invalid criteria format",
                nameof(FederatedCredentialPolicy.Criteria));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    It.IsAny<User>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<FederatedCredentialType>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
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
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var unauthorizedResult = FederatedCredentialPolicyValidationResult.Unauthorized(
                "User does not have permissions",
                nameof(FederatedCredentialPolicy.PackageOwner));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    It.IsAny<User>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<FederatedCredentialType>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
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
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = null, // Null package owner
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"owner":"test","repo":"test"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = UserA,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    UserA,
                    null,
                    """{"owner":"test","repo":"test"}""",
                    "Test Policy",
                    FederatedCredentialType.EntraIdServicePrincipal,
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                UserA,
                null,
                """{"owner":"test","repo":"test"}""",
                "Test Policy",
                FederatedCredentialType.EntraIdServicePrincipal,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()), Times.Once);
        }

        [Fact]
        public async Task WhenValidPolicyWithNullPolicyName_PassesNullToService()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = null, // Null policy name
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"owner":"test","repo":"test"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = UserA,
                    PolicyName = null
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    UserA,
                    OrgA.Username,
                    """{"owner":"test","repo":"test"}""",
                    null,
                    FederatedCredentialType.EntraIdServicePrincipal,
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                UserA,
                OrgA.Username,
                """{"owner":"test","repo":"test"}""",
                null,
                FederatedCredentialType.EntraIdServicePrincipal,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()), Times.Once);
        }

        [Theory]
        [InlineData(FederatedCredentialType.EntraIdServicePrincipal)]
        [InlineData(FederatedCredentialType.GitHubActions)]
        public async Task WhenDifferentPolicyTypes_PassesCorrectTypeToService(FederatedCredentialType policyType)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = policyType,
                PolicyCriteria = """{"test": "value"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = UserA,
                    PolicyName = "Test Policy"
                });

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    UserA,
                    OrgA.Username,
                    """{"test": "value"}""",
                    "Test Policy",
                    policyType,
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                UserA,
                OrgA.Username,
                """{"test": "value"}""",
                "Test Policy",
                policyType,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()), Times.Once);
        }

        [Theory]
        [InlineData("package:push,package:unlist", "policySubject1\r\npolicySubject2")]
        [InlineData(" package:push , package:unlist ", " policySubject1 \r\n policySubject2 ")]
        [InlineData(",package:push,  package:unlist  ", "\r\npolicySubject1\r\n  policySubject2  \r\n")]
        [InlineData("package:push,package:push, package:unlist", "policySubject1\r\npolicySubject2\r\n policySubject2")]
        public async Task WhenDifferentPolicyScopesAndSubjects_BuildScopes(string policyScopes, string policySubjects)
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"tenant":"test","object":"123"}""",
                PolicyScopes = policyScopes,
                PolicySubjects = policySubjects,
            };

            var successResult = FederatedCredentialPolicyValidationResult.Success(
                new FederatedCredentialPolicy
                {
                    Key = 42,
                    CreatedBy = UserA,
                    PolicyName = "Test Policy"
                });

            var passedScopes = Array.Empty<string>();
            var passedSubjects = Array.Empty<string>();
            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    UserA,
                    OrgA.Username,
                    """{"tenant":"test","object":"123"}""",
                    "Test Policy",
                    FederatedCredentialType.EntraIdServicePrincipal,
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                .ReturnsAsync(successResult)
                .Callback((User createdBy, string policyPackageOwner, string policyCriteria, string policyName, FederatedCredentialType policyType, string[] scopes, string[] subjects) =>
                          { passedScopes = scopes; passedSubjects = subjects; });

            // Act
            var result = await Target.CreatePolicy(addPolicy);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains(UserA.Username, redirectResult.Url);
            Assert.Contains("Policy with key 42 added successfully", Target.TempData[$"MessageFor{UserA.Username}"].ToString());

            FederatedCredentialService.Verify(x => x.AddPolicyAsync(
                UserA,
                OrgA.Username,
                """{"tenant":"test","object":"123"}""",
                "Test Policy",
                FederatedCredentialType.EntraIdServicePrincipal,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()), Times.Once);

            Assert.Equal(2, passedScopes.Length);
            Assert.Contains("package:push", passedScopes);
            Assert.Contains("package:unlist", passedScopes);
            Assert.Equal(2, passedSubjects.Length);
            Assert.Contains("policySubject1", passedSubjects);
            Assert.Contains("policySubject2", passedSubjects);
        }

        [Fact]
        public async Task WhenModelErrorMappingForDifferentProperties_MapsCorrectly()
        {
            // Arrange
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "Policy name too long",
                nameof(FederatedCredentialPolicy.PolicyName));

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    It.IsAny<User>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<FederatedCredentialType>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
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
            var addPolicy = new AddPolicyViewModel
            {
                PolicyName = "Test Policy",
                PolicyUser = UserA.Username,
                PolicyPackageOwner = OrgA.Username,
                PolicyType = FederatedCredentialType.EntraIdServicePrincipal,
                PolicyCriteria = """{"test": "value"}""",
                PolicyScopes = $"{NuGetScopes.PackagePush}",
                PolicySubjects = "policySubject1",
            };

            var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest(
                "General error",
                "UnknownProperty");

            FederatedCredentialService
                .Setup(x => x.AddPolicyAsync(
                    It.IsAny<User>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<FederatedCredentialType>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
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
            new FederatedCredentialPolicy { Key = 4, Created = baseTime.AddHours(2), CreatedByUserKey = UserA.Key, CreatedBy = UserA, PackageOwnerUserKey = UserA.Key, PackageOwner = UserA,
                                            Scopes = [ new Scope(UserA, "policySubject1", NuGetScopes.PackagePush) ] },
            new FederatedCredentialPolicy { Key = 5, Created = baseTime.AddHours(1), CreatedByUserKey = UserA.Key, CreatedBy = UserA, PackageOwnerUserKey = OrgA.Key, PackageOwner = OrgA,
                                            Scopes = [ new Scope(OrgA, "policySubject1", NuGetScopes.PackagePush) ] },
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
