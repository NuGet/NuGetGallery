// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

using Moq;

using NuGet.Services.Entities;
using NuGetGallery.Framework;
using NuGetGallery.Services.Authentication;
using Xunit;

namespace NuGetGallery;

public class TheTrustedPublishingAction : TestContainer
{
    public static IEnumerable<object[]> CurrentUserWithTrustedPublishingEnabled_Data =
        MemberDataHelper.AsDataSet(
            TestUtility.FakeUser,
            TestUtility.FakeAdminUser,
            TestUtility.FakeOrganizationAdmin,
            TestUtility.FakeOrganizationCollaborator);

    [Theory]
    [MemberData(nameof(CurrentUserWithTrustedPublishingEnabled_Data))]
    public void WhenTrustedPublishingEnabled_ReturnsTrustedPublishingView(User currentUser)
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(currentUser))
            .Returns(true);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns([]);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(currentUser, It.IsAny<User>()))
            .Returns(true);

        // Act
        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.NotNull(model);
        Assert.Equal(currentUser.Username, model.Username);
        Assert.NotNull(model.PackageOwners);
        Assert.NotNull(model.Policies);

        var firstPackageOwner = model.PackageOwners.FirstOrDefault();
        Assert.Equal(currentUser.Username, firstPackageOwner);
    }

    [Fact]
    public void WhenTrustedPublishingDisabled_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(false);

        var controller = GetController<UsersController>();

        // Act
        var result = controller.TrustedPublishing();

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, ((HttpStatusCodeResult)result).StatusCode);
    }

    [Fact]
    public void FiltersOnlyTrustedPublisherPolicies()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeUser;
        var policies = new List<FederatedCredentialPolicy>
                {
                    new FederatedCredentialPolicy
                    {
                        Key = 1,
                        PolicyName = "Trusted Publisher Policy",
                        PackageOwner = currentUser,
                        Type = FederatedCredentialType.GitHubActions,
                        Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
                    },
                    new FederatedCredentialPolicy
                    {
                        Key = 2,
                        PolicyName = "Other Policy Type",
                        PackageOwner = currentUser,
                        Type = FederatedCredentialType.EntraIdServicePrincipal, // Different type
                        Criteria = """{"foo":"bar"}"""
                    }
                };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        Assert.Equal("Trusted Publisher Policy", model.Policies.First().PolicyName);
    }

    [Fact]
    public void WhenPolicyHasUserNotInOrganizationInvalidReason_CorrectlyPopulatesModel()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeUser;
        var organization = TestUtility.FakeOrganization;

        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Invalid Policy - User Not In Org",
            PackageOwner = organization,
            PackageOwnerUserKey = organization.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        Assert.False(policyViewModel.IsOwnerValid);
        Assert.Equal("Invalid Policy - User Not In Org", policyViewModel.PolicyName);
        Assert.Equal(organization.Username, policyViewModel.Owner);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void WhenPolicyHasOrganizationLockedOrDeletedInvalidReason_CorrectlyPopulatesModel(bool isLocked, bool isDeleted)
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        // Create a new organization and user for this test
        var organization = new Organization { Username = "TestOrganization", Key = 5 };
        var currentUser = Get<Fakes>().CreateUser("TestOrganizationAdmin");
        currentUser.Key = 4;
        currentUser.Organizations = [new Membership { Organization = organization, Member = currentUser }];

        organization.IsDeleted = isDeleted;
        organization.UserStatusKey = isLocked ? UserStatus.Locked : UserStatus.Unlocked;

        var policy = new FederatedCredentialPolicy
        {
            Key = 10,
            PolicyName = "Invalid Policy",
            PackageOwner = organization,
            PackageOwnerUserKey = organization.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        Assert.False(policyViewModel.IsOwnerValid);
        Assert.Equal("Invalid Policy", policyViewModel.PolicyName);
        Assert.Equal(organization.Username, policyViewModel.Owner);
    }

    [Fact]
    public void WhenUserOwnsPolicy_OwnerIsValid()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeOrganizationAdmin;

        var policy = new FederatedCredentialPolicy
        {
            Key = 3,
            PolicyName = "Valid Policy",
            CreatedByUserKey = currentUser.Key,
            CreatedBy = currentUser,
            PackageOwnerUserKey = currentUser.Key,
            PackageOwner = currentUser, // User owns their own policy
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(currentUser, currentUser))
            .Returns(true);

        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        Assert.True(policyViewModel.IsOwnerValid);
        Assert.Equal("Valid Policy", policyViewModel.PolicyName);
        Assert.Equal(currentUser.Username, policyViewModel.Owner);
    }

    [Fact]
    public void WhenOrganizationMemberOwnsPolicy_OwnerIsValid()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeOrganizationAdmin;
        var organization = TestUtility.FakeOrganization;

        var policy = new FederatedCredentialPolicy
        {
            Key = 4,
            PolicyName = "Org Member Policy",
            CreatedBy = currentUser,
            CreatedByUserKey = currentUser.Key,
            PackageOwnerUserKey = organization.Key,
            PackageOwner = organization,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        GetMock<IUserService>()
            .Setup(u => u.FindByKey(organization.Key, false))
            .Returns(organization);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(currentUser, organization))
            .Returns(true);

        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        Assert.True(policyViewModel.IsOwnerValid);
        Assert.Equal("Org Member Policy", policyViewModel.PolicyName);
        Assert.Equal(organization.Username, policyViewModel.Owner);
    }

    [Fact]
    public void WhenPolicyIsPermanent_IsPermanentlyEnabled()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Permanent Policy",
            PackageOwner = currentUser,
            PackageOwnerUserKey = currentUser.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"123","repositoryId":"456"}""" // Has IDs = permanent
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        // Act
        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        var details = (GitHubPolicyDetailsViewModel)policyViewModel.PolicyDetails;
        Assert.True(details.IsPermanentlyEnabled);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(GitHubPolicyDetailsViewModel.ValidationExpirationDays)]
    public void WhenPolicyWithValidateByDate_IsTemporaryAndEnabledDaysLeft(int daysLeft)
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeUser;
        DateTimeOffset validateBy = DateTimeOffset.UtcNow + TimeSpan.FromHours(24 * daysLeft - 1);
        string dbJson = """{"owner":"nuget","repository":"Engineering","workflow":"2.yam","environment":"what","validateBy":"%DATE%"}""";
        dbJson = dbJson.Replace("%DATE%", validateBy.ToString("yyyy-MM-ddTHH:00:00Z"));
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Validate By",
            PackageOwner = currentUser,
            PackageOwnerUserKey = currentUser.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = dbJson
        };

        var policies = new List<FederatedCredentialPolicy> { policy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        // Act
        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Single(model.Policies);
        var policyViewModel = model.Policies.First();
        var details = (GitHubPolicyDetailsViewModel)policyViewModel.PolicyDetails;
        Assert.False(details.IsPermanentlyEnabled);
        Assert.Equal(Math.Max(0, daysLeft), details.EnabledDaysLeft);
    }

    [Fact]
    public void WhenMultiplePoliciesWithDifferentTypes_CorrectlyClassifiesEach()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var currentUser = TestUtility.FakeUser;
        var temporaryPolicy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Temporary Policy",
            PackageOwner = currentUser,
            PackageOwnerUserKey = currentUser.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"","repositoryId":"","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        var permanentPolicy = new FederatedCredentialPolicy
        {
            Key = 2,
            PolicyName = "Permanent Policy",
            PackageOwner = currentUser,
            PackageOwnerUserKey = currentUser.Key,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"123","repositoryId":"456"}"""
        };

        var policies = new List<FederatedCredentialPolicy> { temporaryPolicy, permanentPolicy };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(It.IsAny<int>()))
            .Returns(policies);

        // Act
        var model = GetModelForTrustedPublishing(currentUser);

        // Assert
        Assert.Equal(2, model.Policies.Count);

        var tempPolicyViewModel = model.Policies.First(p => p.PolicyName == "Temporary Policy");
        var tempDetails = (GitHubPolicyDetailsViewModel)tempPolicyViewModel.PolicyDetails;
        Assert.False(tempDetails.IsPermanentlyEnabled);
        Assert.Equal(0, tempDetails.EnabledDaysLeft); // no explicit validateBy date => 0 days left

        var permPolicyViewModel = model.Policies.First(p => p.PolicyName == "Permanent Policy");
        var permDetails = (GitHubPolicyDetailsViewModel)permPolicyViewModel.PolicyDetails;
        Assert.True(permDetails.IsPermanentlyEnabled);
    }

    private TrustedPublisherPolicyListViewModel GetModelForTrustedPublishing(User currentUser)
    {
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(currentUser);

        // Act
        var result = controller.TrustedPublishing();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<TrustedPublisherPolicyListViewModel>(viewResult.Model);
        return viewResult.Model as TrustedPublisherPolicyListViewModel;
    }
}

public class TheGenerateTrustedPublisherPolicyAction : TestContainer
{
    [Fact]
    public async Task WhenValidRequestWithIds_CreatesPermanentlyEnabledPolicy()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        GetMock<IUserService>()
            .Setup(u => u.FindByUsername(user.Username, false))
            .Returns(user);

        string criteria = """{"RepositoryOwner":"repoOwner","RepositoryOwnerId":"123","Repository":"repo","RepositoryId":"456","WorkflowFile":"a.yml"}""";

        var policyResult = FederatedCredentialPolicyValidationResult.Success(
            new FederatedCredentialPolicy
            {
                PolicyName = "Test Policy",
                PackageOwner = user,
                Type = FederatedCredentialType.GitHubActions,
                Criteria = """{"owner":"repoOwner","ownerId":"123","repository":"repo","repositoryId":"456","workflow":"a.yml"}"""
            }
        );

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddPolicyAsync(user, user.Username, It.IsAny<string>(), "Test Policy", FederatedCredentialType.GitHubActions))
            .ReturnsAsync(policyResult);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: criteria);

        // Assert
        var model = (TrustedPublisherPolicyViewModel)result.Data;
        var details = (GitHubPolicyDetailsViewModel)model.PolicyDetails;
        Assert.True(details.IsPermanentlyEnabled);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.AddPolicyAsync(user, user.Username, It.IsAny<string>(), "Test Policy", FederatedCredentialType.GitHubActions), Times.Once);
    }

    [Fact]
    public async Task WhenFederatedCredentialServiceReturnsBadRequest_ReturnsBadRequestWithUserMessage()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        GetMock<IUserService>()
            .Setup(u => u.FindByUsername(user.Username, false))
            .Returns(user);

        var policyResult = FederatedCredentialPolicyValidationResult.BadRequest("Something went wrong", null);

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddPolicyAsync(user, user.Username, It.IsAny<string>(), "Test Policy", FederatedCredentialType.GitHubActions))
            .ReturnsAsync(policyResult);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: """{"RepositoryOwner":"repoOwner","Repository":"repo","WorkflowFile":"a.yml"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal("Something went wrong", (string)result.Data);
    }

    [Fact]
    public async Task WhenFederatedCredentialServiceReturnsUnauthorizes_ReturnsUnauthorizedWithUserMessage()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        GetMock<IUserService>()
            .Setup(u => u.FindByUsername(user.Username, false))
            .Returns(user);

        var policyResult = FederatedCredentialPolicyValidationResult.Unauthorized("Something went wrong", null);

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddPolicyAsync(user, user.Username, It.IsAny<string>(), "Test Policy", FederatedCredentialType.GitHubActions))
            .ReturnsAsync(policyResult);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: """{"RepositoryOwner":"repoOwner","Repository":"repo","WorkflowFile":"a.yml"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.Unauthorized, controller.Response.StatusCode);
        Assert.Equal("Something went wrong", (string)result.Data);
    }
}

public class TheEditTrustedPublisherPolicyAction : TestContainer
{
    [Fact]
    public async Task WhenEditingPolicy_CallsUpdatePolicyAsyncWithCorrectParameters()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        string newJSCriteria = """{"RepositoryOwner":"someOwner","Repository":"repo","WorkflowFile":"new.yml","Environment":"", "validateBy":"2025-01-01T00:00:00Z"}""";

        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"old.yml", "validateBy":"2025-01-01T00:00:00Z"}"""
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        // Mock the service to return a successful result
        var updateResult = FederatedCredentialPolicyValidationResult.Success(policy);
        GetMock<IFederatedCredentialService>()
            .Setup(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), "Test Policy"))
            .ReturnsAsync(updateResult);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, newJSCriteria, "Test Policy");

        // Assert - Focus on controller concerns only
        Assert.IsType<JsonResult>(result);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), "Test Policy"), Times.Once);
    }

    [Fact]
    public async Task WhenEditingPolicy_ReturnsJsonResultFromService()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        string newJSCriteria = """{"RepositoryOwner":"someOwner","Repository":"repo","WorkflowFile":"new.yml", "validateBy":"2025-01-01T00:00:00Z"}""";

        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"old.yml", "validateBy":"2025-01-01T00:00:00Z"}"""
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var updateResult = FederatedCredentialPolicyValidationResult.Success(policy);
        GetMock<IFederatedCredentialService>()
            .Setup(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), "Test Policy"))
            .ReturnsAsync(updateResult);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, newJSCriteria, "Test Policy");

        // Assert - Focus only on controller behavior
        Assert.IsType<JsonResult>(result);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), "Test Policy"), Times.Once);
    }

    [Fact]
    public async Task WhenFederatedCredentialServiceReturnsError_ReturnsBadRequestWithUserMessage()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var badRequestResult = FederatedCredentialPolicyValidationResult.BadRequest("Validation failed", null);
        GetMock<IFederatedCredentialService>()
            .Setup(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), "Test Policy"))
            .ReturnsAsync(badRequestResult);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, """{"test": "value"}""", "Test Policy");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal("Validation failed", (string)result.Data);
    }

    [Fact]
    public async Task WhenPolicyNotFound_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns((FederatedCredentialPolicy)null);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(1, """{"test": "value"}""", "Test Policy");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_Unexpected, (string)result.Data);
    }

    [Fact]
    public async Task WhenUserNotOwnerOfPolicy_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var otherUser = TestUtility.FakeAdminUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = otherUser.Key,
            CreatedBy = otherUser,
            PackageOwnerUserKey = otherUser.Key,
            PackageOwner = otherUser,
            Type = FederatedCredentialType.GitHubActions
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, """{"test": "value"}""", "Test Policy");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.Unauthorized, (string)result.Data);
    }
}

public class TheEnableTrustedPublisherPolicyAction : TestContainer
{
    [Fact]
    public async Task WhenPolicyNotFound_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns((FederatedCredentialPolicy)null);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_Unexpected, (string)result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task WhenFederatedCredentialKeyIsInvalid_ReturnsBadRequest(int? federatedCredentialKey)
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(It.IsAny<int>()))
            .Returns((FederatedCredentialPolicy)null);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: federatedCredentialKey);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_Unexpected, (string)result.Data);
    }

    [Fact]
    public async Task WhenValidPolicy_CallsUpdatePolicyAsyncAndReturnsJsonResult()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","validateBy":"2025-01-01T00:00:00Z"}"""
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var updateResult = FederatedCredentialPolicyValidationResult.Success(policy);
        GetMock<IFederatedCredentialService>()
            .Setup(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), policy.PolicyName))
            .ReturnsAsync(updateResult);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert - Focus on controller behavior only
        Assert.IsType<JsonResult>(result);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, It.IsAny<string>(), policy.PolicyName), Times.Once);
    }
}

public class TheRemoveTrustedPublisherPolicyAction : TestContainer
{
    [Fact]
    public async Task WhenTrustedPublishingDisabled_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(false);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.RemoveTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.DefaultUserSafeExceptionMessage, ((JsonResult)result).Data);
    }

    [Fact]
    public async Task WhenPolicyNotFound_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns((FederatedCredentialPolicy)null);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.RemoveTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_Unexpected, ((JsonResult)result).Data);
    }

    [Fact]
    public async Task WhenUserNotOwnerOfPolicy_ReturnsForbidden()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var otherUser = TestUtility.FakeAdminUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PackageOwner = otherUser,
            PackageOwnerUserKey = otherUser.Key,
            Type = FederatedCredentialType.GitHubActions
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns(policy);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.RemoveTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);
    }

    [Fact]
    public async Task WhenValidRequest_DeletesPolicy()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.DeletePolicyAsync(policy))
            .Returns(Task.CompletedTask);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.RemoveTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal(0, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPolicyRemoved, ((JsonResult)result).Data);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.DeletePolicyAsync(policy), Times.Once);
    }
}
