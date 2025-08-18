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
                        Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
                    },
                    new FederatedCredentialPolicy
                    {
                        Key = 2,
                        PolicyName = "Other Policy Type",
                        PackageOwner = currentUser,
                        Type = FederatedCredentialType.EntraIdServicePrincipal, // Different type
                        Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
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
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
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
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
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
            PackageOwnerUserKey = currentUser.Key,
            PackageOwner = currentUser, // User owns their own policy
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
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
            CreatedByUserKey = currentUser.Key,
            PackageOwnerUserKey = organization.Key,
            PackageOwner = organization,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml"}"""
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
            Criteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"","repositoryId":""}"""
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
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.DefaultUserSafeExceptionMessage, (string)result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task WhenEmptyPolicyNameProvided_ReturnsBadRequest(string policyName)
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: policyName,
            owner: user.Username,
            criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_PolicyNameRequired, (string)result.Data);
    }

    [Fact]
    public async Task WhenPolicyNameTooLong_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        var longPolicyName = new string('a', 300); // Exceeds 128 character limit

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: longPolicyName,
            owner: user.Username,
            criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_NameTooLong, (string)result.Data);
    }

    [Fact]
    public async Task WhenOwnerNotFound_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        GetMock<IUserService>()
            .Setup(u => u.FindByUsername("NonExistentUser", false))
            .Returns((User)null);

        var user = TestUtility.FakeUser;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: "NonExistentUser",
            criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.AddOwner_OwnerNotFound, (string)result.Data);
    }

    [Fact]
    public async Task WhenUserIsLocked_ReturnsBadRequest()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var fakes = Get<Fakes>();
        var user = fakes.CreateUser("user1");
        user.UserStatusKey = UserStatus.Locked;
        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        GetMock<IUserService>()
            .Setup(u => u.FindByUsername(user.Username, false))
            .Returns(user);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(ServicesStrings.UserAccountIsLocked, (string)result.Data);
    }

    [Theory]
    [InlineData("""{"foo":"bar"}""")]
    [InlineData("""{"Repository":"repo", "WorkflowFile":"a.yml"}""")] // no owner
    [InlineData("""{"RepositoryOwner":"repoOwner","Repository":"repo"}""")] // no workflow file
    [InlineData("""{"RepositoryOwner":"repoOwner","WorkflowFile":"a.yml"}""")] // no repository
    public async Task WhenInvalidCriteria_ReturnsBadRequest(string criteria)
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

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: criteria);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.NotEmpty((string)result.Data);
    }

    [Theory]
    [InlineData("""{"RepositoryOwner":"repoOwner","Repository":"repo","RepositoryId":"1","WorkflowFile":"a.yml"}""")] // no owner ID
    [InlineData("""{"RepositoryOwner":"repoOwner","RepositoryOwnerId":"1","Repository":"repo","WorkflowFile":"a.yml"}""")] // no repository ID
    public async Task WhenValidRequestWithoutIds_CreatesTemporaryEnabledPolicy(string criteria)
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

        var policyResult = AddFederatedCredentialPolicyResult.Created(
            new FederatedCredentialPolicy
            {
                PolicyName = "Test Policy",
                PackageOwner = user,
                Type = FederatedCredentialType.GitHubActions,
                Criteria = criteria
            }
        );

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()))
            .ReturnsAsync(policyResult);

        // Act
        var result = await controller.GenerateTrustedPublisherPolicy(
            policyName: "Test Policy",
            owner: user.Username,
            criteria: criteria);

        // Assert
        var model = (TrustedPublisherPolicyViewModel)result.Data;
        var details = (GitHubPolicyDetailsViewModel)model.PolicyDetails;
        Assert.False(details.IsPermanentlyEnabled);
        Assert.Equal(GitHubPolicyDetailsViewModel.ValidationExpirationDays, details.EnabledDaysLeft);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()), Times.Once);
    }

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

        var policyResult = AddFederatedCredentialPolicyResult.Created(
            new FederatedCredentialPolicy
            {
                PolicyName = "Test Policy",
                PackageOwner = user,
                Type = FederatedCredentialType.GitHubActions,
                Criteria = """{"owner":"repoOwner","ownerId":"123","repository":"repo","repositoryId":"456","workflow":"a.yml"}"""
            }
        );

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()))
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
            .Verify(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()), Times.Once);
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

        var policyResult = AddFederatedCredentialPolicyResult.BadRequest("Something went wrong");

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()))
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

        var policyResult = AddFederatedCredentialPolicyResult.Unauthorized("Something went wrong");

        GetMock<IFederatedCredentialService>()
            .Setup(s => s.AddTrustedPublishingPolicyAsync(user, user, "Test Policy", FederatedCredentialType.GitHubActions, It.IsAny<string>()))
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
    public async Task WhenEditingPolicy_FromPermanentToTemporary()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        string oldDBCriteria = """{"owner":"someOwner","repository":"repo","workflow":"old.yml","ownerId":"12","repositoryId":"45"}""";
        string newJSCriteria = """{"RepositoryOwner":"someOwner","Repository":"repo","WorkflowFile":"new.yml","Environment":"","RepositoryOwnerId":"","RepositoryId":""}""";

        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, "Test Policy", newJSCriteria);

        // Assert
        var viewModel = (TrustedPublisherPolicyViewModel)result.Data;
        var details = (GitHubPolicyDetailsViewModel)viewModel.PolicyDetails;
        Assert.False(details.IsPermanentlyEnabled);
        Assert.Equal(GitHubPolicyDetailsViewModel.ValidationExpirationDays, details.EnabledDaysLeft);

        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, "Test Policy", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task WhenEditingPolicy_FromTemporaryToPermanent()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        string oldDBCriteria = """{"owner":"someOwner","repository":"repo","workflow":"old.yml","ownerId":"","repositoryId":""}""";
        string newJSCriteria = """{"RepositoryOwner":"someOwner","RepositoryOwnerId":"12","Repository":"repo","RepositoryId":"45","WorkflowFile":"new.yml","Environment":""}""";

        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, "Test Policy", newJSCriteria);

        // Assert
        var viewModel = (TrustedPublisherPolicyViewModel)result.Data;
        var details = (GitHubPolicyDetailsViewModel)viewModel.PolicyDetails;
        Assert.True(details.IsPermanentlyEnabled);

        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, "Test Policy", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task WhenEditing_ResetsEnabledDaysLeft()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;
        string oldDBCriteria = """{"owner":"someOwner","repository":"repo","workflow":"old.yml","ownerId":"12","repositoryId":"45"}""";
        string newJSCriteria = """{"RepositoryOwner":"someOwner","Repository":"repo","WorkflowFile":"new.yml","Environment":"","RepositoryOwnerId":"","RepositoryId":""}""";

        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPoliciesCreatedByUser(user.Key))
            .Returns([policy]);

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, "Test Policy", newJSCriteria);

        // Assert
        var viewModel = (TrustedPublisherPolicyViewModel)result.Data;
        var details = (GitHubPolicyDetailsViewModel)viewModel.PolicyDetails;
        Assert.False(details.IsPermanentlyEnabled);
        Assert.Equal(GitHubPolicyDetailsViewModel.ValidationExpirationDays, details.EnabledDaysLeft);

        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, "Test Policy", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task WhenEmptyPolicyNameProvided_ReturnsBadRequest(string policyName)
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

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, policyName, criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_PolicyNameRequired, (string)result.Data);
    }

    [Fact]
    public async Task WhenPolicyNameTooLong_ReturnsBadRequest()
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

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        var longPolicyName = new string('a', 65); // Exceeds 64 character limit

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, longPolicyName, criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.TrustedPublisher_NameTooLong, (string)result.Data);
    }

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
        var result = await controller.EditTrustedPublisherPolicy(1, "Test Policy", """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.DefaultUserSafeExceptionMessage, (string)result.Data);
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
        var result = await controller.EditTrustedPublisherPolicy(1, "Test Policy", """{"test": "value"}""");

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
        var result = await controller.EditTrustedPublisherPolicy(123, "Test Policy", criteria: """{"test": "value"}""");

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.Unauthorized, (string)result.Data);
    }

    [Fact]
    public async Task WhenValidRequest_UpdatesPolicy()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        string oldDBCriteria = """{"owner":"someOwner","ownerId":"12","repository":"repo","repositoryId":"45","workflow":"old.yml","environment":"prod"}""";
        string newJSCriteria = """{"RepositoryOwner":"someOwner","RepositoryOwnerId":"12","Repository":"repo","RepositoryId":"45","WorkflowFile":"new.yml","Environment":""}""";

        var user = TestUtility.FakeUser;
        var policy = new FederatedCredentialPolicy
        {
            Key = 123,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(123))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EditTrustedPublisherPolicy(123, "Test Policy", newJSCriteria);

        // Assert
        Assert.IsType<JsonResult>(result);
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, "Test Policy", It.IsAny<string>()), Times.Once);
    }
}

public class TheEnableTrustedPublisherPolicyAction : TestContainer
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
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.DefaultUserSafeExceptionMessage, (string)result.Data);
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
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

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
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
        Assert.Equal(Strings.Unauthorized, (string)result.Data);
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
    public async Task WhenValidRequest_InitializesValidateByDateAndSavesPolicy()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;

        // Create criteria for a temporarily enabled policy (no owner/repo IDs)
        string oldDBCriteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"","repositoryId":""}""";
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.IsType<JsonResult>(result);
        var viewModel = result.Data as TrustedPublisherPolicyViewModel;
        Assert.NotNull(viewModel);
        Assert.Equal("Test Policy", viewModel.PolicyName);
        Assert.Equal(user.Username, viewModel.Owner);

        // Verify that the policy was saved
        GetMock<IFederatedCredentialService>()
            .Verify(s => s.UpdatePolicyAsync(policy, policy.PolicyName, It.IsAny<string>()), Times.Once);

        // Verify that InitialieValidateByDate was called (criteria should be updated)
        Assert.NotEqual(oldDBCriteria, policy.Criteria);
    }

    [Fact]
    public async Task WhenPolicyAlreadyPermanentlyEnabled_Noop()
    {
        // Arrange
        GetMock<IFeatureFlagService>()
            .Setup(f => f.IsTrustedPublishingEnabled(It.IsAny<User>()))
            .Returns(true);

        var user = TestUtility.FakeUser;

        // Create criteria for a permanently enabled policy (with owner/repo IDs)
        string oldDBCriteria = """{"owner":"someOwner","repository":"repo","workflow":"test.yml","ownerId":"123","repositoryId":"456"}""";
        var policy = new FederatedCredentialPolicy
        {
            Key = 1,
            PolicyName = "Test Policy",
            CreatedByUserKey = user.Key,
            PackageOwnerUserKey = user.Key,
            PackageOwner = user,
            Type = FederatedCredentialType.GitHubActions,
            Criteria = oldDBCriteria
        };

        GetMock<IFederatedCredentialService>()
            .Setup(r => r.GetPolicyByKey(1))
            .Returns(policy);

        GetMock<IFederatedCredentialService>()
            .Setup(x => x.IsValidPolicyOwner(It.IsAny<User>(), It.IsAny<User>()))
            .Returns(true);

        var controller = GetController<UsersController>();
        controller.SetCurrentUser(user);

        // Act
        var result = await controller.EnableTrustedPublisherPolicy(federatedCredentialKey: 1);

        // Assert
        Assert.IsType<JsonResult>(result);
        var viewModel = result.Data as TrustedPublisherPolicyViewModel;
        Assert.NotNull(viewModel);
        Assert.Equal("Test Policy", viewModel.PolicyName);
        Assert.Equal(user.Username, viewModel.Owner);
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
