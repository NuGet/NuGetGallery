// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Moq;
using NuGet.ContentModel;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery
{
    public class OrganizationsControllerFacts
        : AccountsControllerFacts<OrganizationsController, Organization, OrganizationAccountViewModel>
    {
        private static readonly Func<Fakes, User> _getFakesUser = (Fakes fakes) => fakes.User;
        private static readonly Func<Fakes, User> _getFakesSiteAdmin = (Fakes fakes) => fakes.Admin;
        private static readonly Func<Fakes, User> _getFakesOrganizationAdmin = (Fakes fakes) => fakes.OrganizationAdmin;
        private static readonly Func<Fakes, User> _getFakesOrganizationCollaborator = (Fakes fakes) => fakes.OrganizationCollaborator;

        public class TheAccountAction : TheAccountBaseAction
        {
            protected override ActionResult InvokeAccount(OrganizationsController controller)
            {
                var accountName = GetAccount(controller).Username;
                return controller.ManageOrganization(accountName);
            }

            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsPartialPermissions_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin, false, true);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator, false, false);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsPartialPermissions_Data))]
            public void WithNonOrganizationAdmin_ReturnsPartialPermissions(Func<Fakes, User> getCurrentUser, bool canManage, bool canManageMemberships)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(getCurrentUser(Fakes));

                // Act
                var result = InvokeAccount(controller);

                // Assert
                var model = ResultAssert.IsView<OrganizationAccountViewModel>(result, "ManageOrganization");
                Assert.Equal(canManage, model.CanManage);
                Assert.Equal(canManageMemberships, model.CanManageMemberships);
            }

            [Fact]
            public void ReturnsPendingMembers()
            {
                // Arrange
                var adminUser = new User("Current user")
                {
                    Roles = new[]
                    {
                        new Role { Name = Constants.AdminRoleName }
                    }
                };

                var organizationOwner = new User("Organization owner")
                {
                    EmailAddress = "owner@example.test"
                };

                var pendingMember = new User("Pending member")
                {
                    EmailAddress = "pending@example.test"
                };

                var organization = new Organization
                {
                    Members = new[]
                    {
                        new Membership
                        {
                            Member = organizationOwner
                        },
                    },
                    MemberRequests = new[]
                    {
                        new MembershipRequest
                        {
                            NewMember = pendingMember
                        }
                    }
                };

                var controller = GetController();
                controller.SetCurrentUser(adminUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername("test-organization", /*includeDeleted:*/ false))
                    .Returns(organization);

                // Act
                var result = controller.ManageOrganization("test-organization");

                // Assert
                var model = ResultAssert.IsView<OrganizationAccountViewModel>(result, "ManageOrganization");

                var members = model.Members.ToList();

                Assert.Equal("Organization owner", members[0].Username);
                Assert.False(members[0].Pending);

                Assert.Equal("Pending member", members[1].Username);
                Assert.True(members[1].Pending);
            }
        }

        public class TheCancelChangeEmailAction : TheCancelChangeEmailBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelChangeEmail(controller, account, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheChangeEmailAction : TheChangeEmailBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeChangeEmail(controller, account, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheChangeEmailSubscriptionAction : TheChangeEmailSubscriptionBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            public static IEnumerable<object[]> UpdatesEmailPreferences_Data => MemberDataHelper.Combine(AllowedCurrentUsers_Data, UpdatesEmailPreferences_DefaultData);

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();

                // Act
                var result = await InvokeChangeEmailSubscription(controller, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmationRequiredAction : TheConfirmationRequiredBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public void WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = InvokeConfirmationRequired(controller, account, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmationRequiredPostAction : TheConfirmationRequiredPostBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeConfirmationRequiredPostAsync(controller, account, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task SendsProperNewAccountMessage()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.EmailAddress = null;
                account.UnconfirmedEmailAddress = "baz@bar.test";

                // Act
                var result = await InvokeConfirmationRequiredPostAsync(controller, account, _getFakesOrganizationAdmin) as HttpStatusCodeResult;

                // Assert
                GetMock<IMessageService>()
                    .Verify(ms => ms.SendMessageAsync(It.IsAny<EmailChangeConfirmationMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
                GetMock<IMessageService>()
                    .Verify(ms => ms.SendMessageAsync(It.IsAny<NewAccountMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
            }

            [Fact]
            public async Task ResendsProperConfirmationEmail()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                account.EmailAddress = "foo@bar.test";
                account.UnconfirmedEmailAddress = "baz@bar.test";

                // Act
                var result = await InvokeConfirmationRequiredPostAsync(controller, account, _getFakesOrganizationAdmin) as HttpStatusCodeResult;

                // Assert
                GetMock<IMessageService>()
                    .Verify(ms => ms.SendMessageAsync(It.IsAny<EmailChangeConfirmationMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
                GetMock<IMessageService>()
                    .Verify(ms => ms.SendMessageAsync(It.IsAny<NewAccountMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }
        }

        public class TheConfirmAction : TheConfirmBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeConfirm(controller, account, getCurrentUser);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.WrongUsername);
                Assert.False(model.SuccessfulConfirmation);
            }
        }

        public class TheAddAction : TestContainer
        {
            private const string OrgName = "TestOrg";
            private const string OrgEmail = "TestOrg@testorg.com";

            private readonly AddOrganizationViewModel Model =
                new AddOrganizationViewModel { OrganizationName = OrgName, OrganizationEmailAddress = OrgEmail };

            private readonly User Admin;

            private Fakes Fakes;

            public TheAddAction()
            {
                Fakes = Get<Fakes>();
                Admin = Fakes.User;
            }

            [Fact]
            public async Task WhenAddOrganizationThrowsEntityException_ReturnsViewWithMessage()
            {
                var message = "message";

                var mockUserService = GetMock<IUserService>();
                mockUserService
                    .Setup(x => x.AddOrganizationAsync(OrgName, OrgEmail, Admin))
                    .Throws(new EntityException(message));

                var controller = GetController<OrganizationsController>();
                controller.SetCurrentUser(Admin);

                var result = await controller.Add(Model);

                ResultAssert.IsView<AddOrganizationViewModel>(result);

                Assert.Equal(message, controller.TempData["AddOrganizationErrorMessage"]);

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationAdded(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenAddOrganizationSucceeds_RedirectsToManageOrganization()
            {
                var token = "token";
                var org = new Organization("newlyCreated")
                {
                    UnconfirmedEmailAddress = OrgEmail,
                    EmailConfirmationToken = token
                };

                var mockUserService = GetMock<IUserService>();
                mockUserService
                    .Setup(x => x.AddOrganizationAsync(OrgName, OrgEmail, Admin))
                    .Returns(Task.FromResult(org));

                NewAccountMessage sentMessage = null;
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(It.IsAny<NewAccountMessage>(), false, false))
                    .Callback<IEmailBuilder, bool, bool>(
                        (msg, copySender, discloseSenderAddress) =>
                        {
                            sentMessage = msg as NewAccountMessage;
                        })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = GetController<OrganizationsController>();
                controller.SetCurrentUser(Admin);

                var result = await controller.Add(Model);

                ResultAssert.IsRedirectToRoute(result,
                    new { accountName = org.Username, action = nameof(OrganizationsController.ManageOrganization) });

                Assert.NotNull(sentMessage);
                Assert.Equal(org, sentMessage.User);
                Assert.Contains(token, sentMessage.ConfirmationUrl);

                GetMock<ITelemetryService>()
                    .Verify(t => t.TrackOrganizationAdded(It.IsAny<Organization>()));
            }
        }

        public class TheAddMemberAction : AccountsControllerTestContainer
        {
            private const string DefaultMemberName = "member";

            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            public static IEnumerable<object[]> AllowedCurrentUsers_IsAdmin_Data => MemberDataHelper.Combine(
                AllowedCurrentUsers_Data,
                new[] { MemberDataHelper.AsData(false), MemberDataHelper.AsData(true) });

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, result.Data);

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenOrganizationIsNotConfirmed_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                account.EmailAddress = null;

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Member_OrganizationUnconfirmed, result.Data);

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task WhenEntityException_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser, isAdmin: isAdmin,
                    exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", result.Data);

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(account, DefaultMemberName, isAdmin), Times.Once);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<OrganizationMembershipRequestInitiatedMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.RequestingUser == controller.GetCurrentUser()
                            && msg.PendingUser == It.Is<User>(u => u.Username == DefaultMemberName)
                            && msg.IsAdmin == isAdmin),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                account.EmailAddress = "hello@test.example";

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                GetMock<IMessageService>()
                    .Setup(m => m.SendMessageAsync(
                        It.Is<OrganizationMembershipRequestMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.NewUser == It.Is<User>(u => u.Username == DefaultMemberName)
                            && msg.AdminUser == controller.GetCurrentUser()
                            && msg.IsAdmin == isAdmin),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Assert.Equal(DefaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);
                Assert.Equal(true, data.Pending);
                Assert.Equal(
                    "https://secure.gravatar.com/avatar/a22526ad5b00a9a99b440ed239dbdbad?s=32&r=g&d=retro",
                    data.GravatarUrl);

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(account, DefaultMemberName, isAdmin), Times.Once);
                messageService
                    .Verify(s => s.SendMessageAsync(
                        It.IsAny<OrganizationMembershipRequestInitiatedMessage>(),
                        false,
                        false));
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task ReturnsAvatarProxyUrl(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                GetMock<IFeatureFlagService>()
                    .Setup(f => f.IsGravatarProxyEnabled())
                    .Returns(true);

                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();

                account.EmailAddress = "hello@test.example";

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Assert.Equal(
                    $"/profiles/{data.Username}/avatar?imageSize=32",
                    data.GravatarUrl);
            }

            private Task<JsonResult> InvokeAddMember(
                OrganizationsController controller,
                Organization account,
                Func<Fakes, User> getCurrentUser,
                string memberName = DefaultMemberName,
                bool isAdmin = false,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.AddMembershipRequestAsync(It.IsAny<Organization>(), memberName, isAdmin));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var request = new MembershipRequest
                    {
                        Organization = account,
                        NewMember = new User(memberName),
                        IsAdmin = isAdmin,
                        ConfirmationToken = "token"
                    };

                    request.NewMember.EmailAddress = $"{memberName}@test.example";

                    setup.Returns(Task.FromResult(request)).Verifiable();
                }

                // Act
                return controller.AddMember(account.Username, memberName, isAdmin);
            }
        }

        public class TheConfirmMemberRequestRedirectAction : AccountsControllerTestContainer
        {
            private const string defaultConfirmationToken = "token";

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task RedirectsAndDoesNotPerformWriteOperation(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();

                // Act
                var result = await InvokeConfirmMember(controller, account, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsRedirectTo(result, "/account/Organizations");

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Never);
                messageService.Verify(
                    s => s.SendMessageAsync(
                        It.IsAny<OrganizationMemberUpdatedMessage>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Never);
            }

            private Task<ActionResult> InvokeConfirmMember(
                OrganizationsController controller,
                Organization account,
                bool isAdmin,
                string confirmationToken = defaultConfirmationToken,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(Fakes.User);

                var currentUser = controller.GetCurrentUser();

                var userService = GetMock<IUserService>();
                if (account != null)
                {
                    userService.Setup(u => u.FindByUsername(account.Username, false))
                        .Returns(account as User);
                }
                var setup = userService.Setup(u => u.AddMemberAsync(It.IsAny<Organization>(), currentUser.Username, confirmationToken));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var membership = new Membership
                    {
                        Organization = account,
                        Member = currentUser,
                        IsAdmin = isAdmin,
                    };
                    setup.Returns(Task.FromResult(membership)).Verifiable();
                }

                // Act
                return controller.ConfirmMemberRequestRedirect(account?.Username, confirmationToken);
            }
        }

        public class TheConfirmMemberAction : AccountsControllerTestContainer
        {
            private const string defaultConfirmationToken = "token";

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAccountMissingReturns404(bool isAdmin)
            {
                // Arrange
                var controller = GetController();

                // Act
                var result = await InvokeConfirmMember(controller, account: null, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(It.IsAny<Organization>(), Fakes.User.Username, defaultConfirmationToken), Times.Never);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMemberUpdatedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenEntityException_ReturnsNonSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var failureReason = "error";

                // Act
                var result = await InvokeConfirmMember(controller, account, isAdmin: isAdmin,
                    exception: new EntityException(failureReason));

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<HandleOrganizationMembershipRequestModel>(viewResult.Model);
                Assert.True(model.Confirm);
                Assert.Equal(failureReason, model.FailureReason);
                Assert.Equal(account.Username, model.OrganizationName);
                Assert.False(model.Successful);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMemberUpdatedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<OrganizationMemberUpdatedMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.Membership == It.Is<Membership>(m => Fakes.User.Username == m.Member.Username && m.Organization == account && m.IsAdmin == isAdmin)),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await InvokeConfirmMember(controller, account, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsRedirectTo(result,
                    controller.Url.ManageMyOrganization(account.Username));


                Assert.Equal(String.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_Success, account.Username),
                    controller.TempData["Message"]);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                messageService
                    .Verify(s => s.SendMessageAsync(
                        It.IsAny<OrganizationMemberUpdatedMessage>(),
                        false,
                        false),
                        Times.Once);
            }

            private Task<ActionResult> InvokeConfirmMember(
                OrganizationsController controller,
                Organization account,
                bool isAdmin,
                string confirmationToken = defaultConfirmationToken,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(Fakes.User);

                var currentUser = controller.GetCurrentUser();

                var userService = GetMock<IUserService>();
                if (account != null)
                {
                    userService.Setup(u => u.FindByUsername(account.Username, false))
                        .Returns(account as User);
                }
                var setup = userService.Setup(u => u.AddMemberAsync(It.IsAny<Organization>(), currentUser.Username, confirmationToken));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var membership = new Membership
                    {
                        Organization = account,
                        Member = currentUser,
                        IsAdmin = isAdmin,
                    };
                    setup.Returns(Task.FromResult(membership)).Verifiable();
                }

                // Act
                return controller.ConfirmMemberRequest(account?.Username, confirmationToken);
            }
        }

        public class TheRejectMemberRequestRedirectAction : AccountsControllerTestContainer
        {
            private const string defaultConfirmationToken = "token";

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task RedirectsAndDoesNotPerformWriteOperation(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();

                // Act
                var result = await InvokeRejectMember(controller, account, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsRedirectTo(result, "/account/Organizations");

                GetMock<IUserService>().Verify(
                    s => s.RejectMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Never);
                messageService.Verify(
                    s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestDeclinedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            private Task<ActionResult> InvokeRejectMember(
                OrganizationsController controller,
                Organization account,
                bool isAdmin,
                string confirmationToken = defaultConfirmationToken,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(Fakes.User);

                var currentUser = controller.GetCurrentUser();

                var userService = GetMock<IUserService>();
                if (account != null)
                {
                    userService.Setup(u => u.FindByUsername(account.Username, false))
                        .Returns(account as User);
                }
                var setup = userService.Setup(u => u.RejectMembershipRequestAsync(It.IsAny<Organization>(), currentUser.Username, confirmationToken));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var membership = new Membership
                    {
                        Organization = account,
                        Member = currentUser,
                        IsAdmin = isAdmin,
                    };
                    setup.Returns(Task.CompletedTask).Verifiable();
                }

                // Act
                return controller.RejectMemberRequestRedirect(account?.Username, confirmationToken);
            }
        }

        public class TheRejectMemberAction : AccountsControllerTestContainer
        {
            private const string defaultConfirmationToken = "token";

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenAccountMissingReturns404(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var messageService = GetMock<IMessageService>();

                // Act
                var result = await InvokeRejectMember(controller, account: null, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);

                GetMock<IUserService>().Verify(s => s.RejectMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                messageService
                    .Verify(
                    s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestDeclinedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenEntityException_ReturnsNonSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var failureReason = "error";

                // Act
                var result = await InvokeRejectMember(controller, account, isAdmin: isAdmin,
                    exception: new EntityException(failureReason));

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<HandleOrganizationMembershipRequestModel>(viewResult.Model);
                Assert.False(model.Confirm);
                Assert.Equal(failureReason, model.FailureReason);
                Assert.Equal(account.Username, model.OrganizationName);
                Assert.False(model.Successful);

                GetMock<IUserService>().Verify(s => s.RejectMembershipRequestAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                GetMock<IMessageService>()
                    .Verify(
                    s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestDeclinedMessage>(), It.IsAny<bool>(), It.IsAny<bool>()),
                    Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<OrganizationMembershipRequestDeclinedMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.PendingUser == Fakes.User),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await InvokeRejectMember(controller, account, isAdmin: isAdmin);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<HandleOrganizationMembershipRequestModel>(viewResult.Model);
                Assert.False(model.Confirm);
                Assert.Equal(account.Username, model.OrganizationName);
                Assert.True(model.Successful);

                GetMock<IUserService>().Verify(s => s.RejectMembershipRequestAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                messageService.Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestDeclinedMessage>(), false, false), Times.Once);
            }

            private Task<ActionResult> InvokeRejectMember(
                OrganizationsController controller,
                Organization account,
                bool isAdmin,
                string confirmationToken = defaultConfirmationToken,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(Fakes.User);

                var currentUser = controller.GetCurrentUser();

                var userService = GetMock<IUserService>();
                if (account != null)
                {
                    userService.Setup(u => u.FindByUsername(account.Username, false))
                        .Returns(account as User);
                }
                var setup = userService.Setup(u => u.RejectMembershipRequestAsync(It.IsAny<Organization>(), currentUser.Username, confirmationToken));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var membership = new Membership
                    {
                        Organization = account,
                        Member = currentUser,
                        IsAdmin = isAdmin,
                    };
                    setup.Returns(Task.CompletedTask).Verifiable();
                }

                // Act
                return controller.RejectMemberRequest(account?.Username, confirmationToken);
            }
        }

        public class TheUpdateMemberAction : AccountsControllerTestContainer
        {
            private const string defaultMemberName = "member";

            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            public static IEnumerable<object[]> AllowedCurrentUsers_IsAdmin_Data => MemberDataHelper.Combine(
                AllowedCurrentUsers_Data,
                new[] { MemberDataHelper.AsData(false), MemberDataHelper.AsData(true) });

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, result.Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenOrganizationIsUnconfirmed_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                account.EmailAddress = null;

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Member_OrganizationUnconfirmed, result.Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task WhenEntityException_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser, isAdmin: isAdmin,
                    exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", result.Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task WhenMembershipCreated_ReturnsSuccess(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<OrganizationMemberUpdatedMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.Membership == It.Is<Membership>(m => m.Organization == account && m.Member.Username == defaultMemberName && m.IsAdmin == isAdmin)),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Assert.Equal(defaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);
                Asset.Equals(
                    "https://secure.gravatar.com/avatar/a22526ad5b00a9a99b440ed239dbdbad?s=32&r=g&d=retro",
                    data.GravatarUrl);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(account, defaultMemberName, isAdmin), Times.Once);

                messageService
                   .Verify(s => s.SendMessageAsync(
                       It.IsAny<OrganizationMemberUpdatedMessage>(),
                       false,
                       false),
                       Times.Once);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task ReturnsAvatarProxyUrl(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
	            GetMock<IFeatureFlagService>()
                    .Setup(f => f.IsGravatarProxyEnabled())
                    .Returns(true);
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Asset.Equals(
                    $"/profiles/{data.Username}/avatar?imageSize=32",
                    data.GravatarUrl);
            }

            private Task<JsonResult> InvokeUpdateMember(
                OrganizationsController controller,
                Organization account,
                Func<Fakes, User> getCurrentUser,
                string memberName = defaultMemberName,
                bool isAdmin = false,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.UpdateMemberAsync(It.IsAny<Organization>(), memberName, isAdmin));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    var membership = new Membership
                    {
                        Organization = account,
                        Member = new User(memberName),
                        IsAdmin = isAdmin
                    };

                    membership.Member.EmailAddress = $"{memberName}@test.example";
                    setup.Returns(Task.FromResult(membership)).Verifiable();
                }

                // Act
                return controller.UpdateMember(account.Username, memberName, isAdmin);
            }
        }

        public class TheDeleteMemberAction : AccountsControllerTestContainer
        {
            private const string defaultMemberName = "member";

            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeDeleteMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, result.Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(It.IsAny<Organization>(), It.IsAny<string>()), Times.Never);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMemberRemovedMessage>(), false, false), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenOrganizationIsUnconfirmed_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                account.EmailAddress = null;

                // Act
                var result = await InvokeDeleteMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Member_OrganizationUnconfirmed, result.Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(It.IsAny<Organization>(), It.IsAny<string>()), Times.Never);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMemberRemovedMessage>(), false, false), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenEntityException_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeDeleteMember(controller, account, getCurrentUser, exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", result.Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(account, defaultMemberName), Times.Once);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMemberRemovedMessage>(), false, false), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenDeletingAsAdmin_ReturnsSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<OrganizationMemberRemovedMessage>(
                            msg =>
                            msg.Organization == It.Is<User>(u => u.Username == defaultMemberName)),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await InvokeDeleteMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.DeleteMember_Success, result.Data);

                GetMock<IUserService>()
                    .Verify(s => s.DeleteMemberAsync(account, defaultMemberName), Times.Once);
                messageService
                    .Verify(s => s.SendMessageAsync(
                        It.IsAny<OrganizationMemberRemovedMessage>(),
                        false,
                        false),
                        Times.Once);
            }

            [Fact]
            public async Task WhenDeletingSelfAsCollaborator_ReturnsSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                Func<Fakes, User> getCollaborator = (Fakes fakes) => fakes.OrganizationCollaborator;
                var collaborator = getCollaborator(Fakes);
                controller.SetCurrentUser(collaborator);

                // Act
                var result = await InvokeDeleteMember(controller, account, getCollaborator, memberName: collaborator.Username);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.DeleteMember_Success, (result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(account, collaborator.Username), Times.Once);
            }

            private Task<JsonResult> InvokeDeleteMember(
                OrganizationsController controller,
                Organization account,
                Func<Fakes, User> getCurrentUser,
                string memberName = defaultMemberName,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.DeleteMemberAsync(account, memberName));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    setup.Returns(Task.FromResult(new User(memberName))).Verifiable();
                }

                // Act
                return controller.DeleteMember(account.Username, memberName);
            }
        }

        public class TheCancelMemberRequestAction : AccountsControllerTestContainer
        {
            private const string defaultMemberName = "member";

            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesSiteAdmin);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationAdmin);
                }
            }

            public static IEnumerable<object[]> WithNonOrganizationAdmin_ReturnsForbidden_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                    yield return MemberDataHelper.AsData(_getFakesOrganizationCollaborator);
                }
            }

            [Theory]
            [MemberData(nameof(WithNonOrganizationAdmin_ReturnsForbidden_Data))]
            public async Task WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelMemberRequestMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, result.Data);

                GetMock<IUserService>().Verify(s => s.CancelMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>()), Times.Never);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestCanceledMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenEntityException_ReturnsNonSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelMemberRequestMember(controller, account, getCurrentUser, exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", result.Data);

                GetMock<IUserService>().Verify(s => s.CancelMembershipRequestAsync(account, defaultMemberName), Times.Once);
                GetMock<IMessageService>().Verify(s => s.SendMessageAsync(It.IsAny<OrganizationMembershipRequestCanceledMessage>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenSuccess_ReturnsSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(s => s.SendMessageAsync(
                        It.Is<OrganizationMembershipRequestCanceledMessage>(
                            msg =>
                            msg.Organization == account
                            && msg.PendingUser == It.Is<User>(u => u.Username == defaultMemberName)),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await InvokeCancelMemberRequestMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.CancelMemberRequest_Success, result.Data);

                GetMock<IUserService>().Verify(s => s.CancelMembershipRequestAsync(account, defaultMemberName), Times.Once);
                messageService.Verify(s => s.SendMessageAsync(
                    It.IsAny<OrganizationMembershipRequestCanceledMessage>(),
                    false,
                    false),
                    Times.Once);
            }

            private Task<JsonResult> InvokeCancelMemberRequestMember(
                OrganizationsController controller,
                Organization account,
                Func<Fakes, User> getCurrentUser,
                string memberName = defaultMemberName,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(getCurrentUser(Fakes));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username, false))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.CancelMembershipRequestAsync(account, memberName));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    setup.Returns(Task.FromResult(new User(memberName))).Verifiable();
                }

                // Act
                return controller.CancelMemberRequest(account.Username, memberName);
            }
        }

        public abstract class TheDeleteOrganizationBaseAction : TestContainer
        {
            public static IEnumerable<object[]> IfNotAdministrator_ReturnsNotFound_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(new Func<Fakes, User>(fakes => fakes.User));
                    yield return MemberDataHelper.AsData(new Func<Fakes, User>(fakes => fakes.Admin));
                    yield return MemberDataHelper.AsData(new Func<Fakes, User>(fakes => fakes.OrganizationCollaborator));
                }
            }

            [Theory]
            [MemberData(nameof(IfNotAdministrator_ReturnsNotFound_Data))]
            public async Task IfNotAdministrator_ReturnsNotFound(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.Organization;

                controller.SetCurrentUser(getCurrentUser(fakes));

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(testOrganization.Username, false))
                    .Returns(testOrganization);

                // Act
                var result = await Invoke(controller, testOrganization.Username);

                // Assert
                ResultAssert.IsNotFound(result);
            }

            protected abstract Task<ActionResult> Invoke(OrganizationsController controller, string username);
        }

        public class TheDeleteAccountRequestAction : TheDeleteOrganizationBaseAction
        {
            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task IfAdministrator_ShowsViewWithCorrectData(bool isPackageOrphaned, bool withAdditionalMembers)
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.Organization;

                controller.SetCurrentUser(fakes.OrganizationAdmin);
                PackageRegistration packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(testOrganization);

                if (!withAdditionalMembers)
                {
                    testOrganization.Members.Remove(fakes.OrganizationCollaborator.Organizations.Single());
                }

                Package userPackage = new Package()
                {
                    Description = "TestPackage",
                    Key = 1,
                    Version = "1.0.0",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(userPackage);

                List<Package> userPackages = new List<Package>() { userPackage };

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(testOrganization.Username, false))
                    .Returns(testOrganization);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testOrganization, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<IPackageService>()
                    .Setup(stub => stub.WillPackageBeOrphanedIfOwnerRemoved(packageRegistration, testOrganization))
                    .Returns(isPackageOrphaned);

                // act
                var result = await Invoke(controller, testOrganization.Username);

                // Assert
                var model = ResultAssert.IsView<DeleteOrganizationViewModel>(result, "DeleteAccount");
                Assert.Equal(testOrganization.Username, model.AccountName);
                Assert.Single(model.Packages);
                Assert.Equal(isPackageOrphaned, model.HasPackagesThatWillBeOrphaned);
                Assert.Equal(withAdditionalMembers, model.HasAdditionalMembers);
            }

            protected override Task<ActionResult> Invoke(OrganizationsController controller, string username)
            {
                return Task.FromResult(controller.DeleteRequest(username));
            }
        }

        public class TheRequestAccountDeletionMethod : TheDeleteOrganizationBaseAction
        {
            [Fact]
            public async Task IfOrphanedPackages_RedirectsToDeleteRequest()
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.OrganizationOwner;
                controller.SetCurrentUser(fakes.OrganizationOwnerAdmin);

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesByAnyMatchingOwner(testOrganization, true, false))
                    .Returns(new[] { new Package { Version = "1.0.0", PackageRegistration = new PackageRegistration { Owners = new[] { testOrganization } } } });
                GetMock<IPackageService>()
                    .Setup(x => x.WillPackageBeOrphanedIfOwnerRemoved(It.IsAny<PackageRegistration>(), testOrganization))
                    .Returns(true);

                // Act & Assert
                await RedirectsToDeleteRequest(
                    controller,
                    testOrganization.Username,
                    "You cannot delete your organization unless you transfer ownership of all of its packages to another account.");
            }

            [Fact]
            public async Task IfAdditionalMembers_RedirectsToDeleteRequest()
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.Organization;
                controller.SetCurrentUser(fakes.OrganizationAdmin);

                // Act & Assert
                await RedirectsToDeleteRequest(
                    controller,
                    testOrganization.Username,
                    "You cannot delete your organization unless you remove all other members.");
            }

            [Fact]
            public async Task IfDeleteFails_RedirectsToDeleteRequest()
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.Organization;
                var currentUser = fakes.OrganizationAdmin;
                controller.SetCurrentUser(currentUser);

                testOrganization.Members.Remove(fakes.OrganizationCollaborator.Organizations.Single());

                GetMock<IDeleteAccountService>()
                    .Setup(x => x.DeleteAccountAsync(testOrganization, currentUser, AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans))
                    .Returns(Task.FromResult(new DeleteAccountStatus { Success = false }));

                // Act & Assert
                await RedirectsToDeleteRequest(
                    controller,
                    testOrganization.Username,
                    $"There was an issue deleting your organization '{testOrganization.Username}'. Please contact support for assistance.");
            }

            [Fact]
            public async Task IfDeleteSucceeds_RedirectsToManageOrganizations()
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testOrganization = fakes.Organization;
                var currentUser = fakes.OrganizationAdmin;
                controller.SetCurrentUser(currentUser);

                testOrganization.Members.Remove(fakes.OrganizationCollaborator.Organizations.Single());

                GetMock<IDeleteAccountService>()
                    .Setup(x => x.DeleteAccountAsync(testOrganization, currentUser, AccountDeletionOrphanPackagePolicy.DoNotAllowOrphans))
                    .Returns(Task.FromResult(new DeleteAccountStatus { Success = true }));

                // Act
                var result = await Invoke(controller, testOrganization.Username);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = nameof(UsersController.Organizations), controller = "Users" });
                Assert.Equal($"Your organization, '{testOrganization.Username}', was successfully deleted!", controller.TempData["Message"]);
            }

            protected override async Task<ActionResult> Invoke(OrganizationsController controller, string username)
            {
                return await controller.RequestAccountDeletion(username);
            }

            private async Task RedirectsToDeleteRequest(OrganizationsController controller, string username, string errorMessage)
            {
                var result = await Invoke(controller, username);
                ResultAssert.IsRedirectToRoute(result, new { action = nameof(OrganizationsController.DeleteRequest) });
                Assert.Equal(errorMessage, controller.TempData["ErrorMessage"]);
            }
        }

        public class TheGetCertificateAction : AccountsControllerTestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly Mock<IUserService> _userService;
            private readonly OrganizationsController _controller;
            private readonly Organization _organization;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheGetCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _userService = GetMock<IUserService>();
                _controller = GetController<OrganizationsController>();
                _organization = new Organization()
                {
                    Key = 1,
                    Username = "a"
                };
                _user = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                _certificate = new Certificate()
                {
                    Key = 3,
                    Thumbprint = "d"
                };

                _organization.Members.Add(new Membership()
                {
                    MemberKey = _user.Key,
                    Member = _user,
                    OrganizationKey = _organization.Key,
                    Organization = _organization,
                    IsAdmin = true
                });

                _userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _organization.Username), false))
                    .Returns(_organization);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void GetCertificate_WhenThumbprintIsInvalid_ReturnsBadRequest(string thumbprint)
            {
                var response = _controller.GetCertificate(_organization.Username, thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.GetCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificate_WhenOrganizationIsNotFound_ReturnsNotFound()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.GetCertificate(
                    accountName: "nonexistent",
                    thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.NotFound, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificate_WhenCurrentUserLacksPermission_ReturnsForbidden()
            {
                var nonmember = new User()
                {
                    Key = 4,
                    Username = "e"
                };

                _controller.SetCurrentUser(nonmember);

                var response = _controller.GetCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void GetCertificate_WhenOrganizationHasNoCertificates_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(Enumerable.Empty<Certificate>());
                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.GetCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Empty((IEnumerable<ListCertificateItemViewModel>)response.Data);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void GetCertificate_WhenOrganizationHasCertificate_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.GetCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.True(viewModel.CanDelete);
                Assert.Equal($"/organization/{_organization.Username}/certificates/{_certificate.Thumbprint}", viewModel.DeleteUrl);
                Assert.Equal(_certificate.Thumbprint, viewModel.Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void GetCertificate_WhenOrganizationHasCertificateAndCurrentUserIsOrganizationCollaborator_ReturnsOK()
            {
                _organization.Members.Single().IsAdmin = false;
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);

                var response = _controller.GetCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.False(viewModel.CanDelete);
                Assert.Null(viewModel.DeleteUrl);
                Assert.Equal(_certificate.Thumbprint, viewModel.Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }
        }

        public class TheGetCertificatesAction : AccountsControllerTestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly Mock<IUserService> _userService;
            private readonly OrganizationsController _controller;
            private readonly Organization _organization;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheGetCertificatesAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _userService = GetMock<IUserService>();
                _controller = GetController<OrganizationsController>();
                _organization = new Organization()
                {
                    Key = 1,
                    Username = "a"
                };
                _user = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                _certificate = new Certificate()
                {
                    Key = 3,
                    Thumbprint = "d"
                };

                _organization.Members.Add(new Membership()
                {
                    MemberKey = _user.Key,
                    Member = _user,
                    OrganizationKey = _organization.Key,
                    Organization = _organization,
                    IsAdmin = true
                });

                _userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _organization.Username), false))
                    .Returns(_organization);
            }

            [Fact]
            public void GetCertificates_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.GetCertificates(_organization.Username);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificates_WhenOrganizationIsNotFound_ReturnsNotFound()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.GetCertificates(accountName: "nonexistent");

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.NotFound, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificates_WhenCurrentUserLacksPermission_ReturnsForbidden()
            {
                var nonmember = new User()
                {
                    Key = 4,
                    Username = "e"
                };

                _controller.SetCurrentUser(nonmember);

                var response = _controller.GetCertificates(_organization.Username);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void GetCertificates_WhenOrganizationHasNoCertificates_ForAllValidClaims_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(Enumerable.Empty<Certificate>());
                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.GetCertificates(_organization.Username);

                Assert.NotNull(response);
                Assert.Empty((IEnumerable<ListCertificateItemViewModel>)response.Data);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void GetCertificates_WhenOrganizationHasCertificate_ForAllValidClaims_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.GetCertificates(_organization.Username);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.True(viewModel.CanDelete);
                Assert.Equal($"/organization/{_organization.Username}/certificates/{_certificate.Thumbprint}", viewModel.DeleteUrl);
                Assert.Equal(_certificate.Thumbprint, viewModel.Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void GetCertificates_WhenOrganizationHasCertificateAndCurrentUserIsOrganizationCollaborator_ReturnsOK()
            {
                _organization.Members.Single().IsAdmin = false;
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _organization)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);

                var response = _controller.GetCertificates(_organization.Username);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.False(viewModel.CanDelete);
                Assert.Null(viewModel.DeleteUrl);
                Assert.Equal(_certificate.Thumbprint, viewModel.Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }
        }

        public class TheAddCertificateAction : AccountsControllerTestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly Mock<ISecurityPolicyService> _securityPolicyService;
            private readonly Mock<IUserService> _userService;
            private readonly OrganizationsController _controller;
            private readonly Mock<IPackageService> _packageService;
            private readonly Organization _organization;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheAddCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _securityPolicyService = GetMock<ISecurityPolicyService>();
                _userService = GetMock<IUserService>();
                _packageService = GetMock<IPackageService>();
                _controller = GetController<OrganizationsController>();
                _organization = new Organization()
                {
                    Key = 1,
                    Username = "a"
                };
                _user = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                _certificate = new Certificate()
                {
                    Key = 3,
                    Thumbprint = "d"
                };

                _organization.Members.Add(new Membership()
                {
                    MemberKey = _user.Key,
                    Member = _user,
                    OrganizationKey = _organization.Key,
                    Organization = _organization,
                    IsAdmin = true
                });

                _userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _organization.Username), false))
                    .Returns(_organization);
            }

            [Fact]
            public void AddCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var uploadFile = new StubHttpPostedFile(contentLength: 0, fileName: "a.cer", inputStream: Stream.Null);
                var response = _controller.AddCertificate(_organization.Username, uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void AddCertificate_WhenOrganizationIsNotFound_ReturnsNotFound()
            {
                var uploadFile = GetUploadFile();

                _controller.SetCurrentUser(_user);

                var response = _controller.AddCertificate(accountName: "nonexistent", uploadFile: uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.NotFound, _controller.Response.StatusCode);
            }

            [Fact]
            public void AddCertificate_WhenCurrentUserLacksPermission_ReturnsForbidden()
            {
                var uploadFile = GetUploadFile();
                var nonmember = new User()
                {
                    Key = 4,
                    Username = "e"
                };

                _controller.SetCurrentUser(nonmember);

                var response = _controller.AddCertificate(_organization.Username, uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Fact]
            public void AddCertificate_WhenCurrentUserIsNotMultiFactorAuthenticated_ReturnsForbidden()
            {
                var uploadFile = GetUploadFile();

                _controller.SetCurrentUser(_user);

                var response = _controller.AddCertificate(_organization.Username, uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void AddCertificate_WhenUploadFileIsNull_ReturnsBadRequest()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.AddCertificate(_organization.Username, uploadFile: null);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void AddCertificate_WhenUploadFileIsValid_ForAllValidClaims_ReturnsCreated(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                var uploadFile = GetUploadFile();

                _certificateService.Setup(x => x.AddCertificateAsync(
                        It.Is<HttpPostedFileBase>(file => ReferenceEquals(file, uploadFile))))
                    .ReturnsAsync(_certificate);
                _certificateService.Setup(x => x.ActivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _organization)))
                    .Returns(Task.CompletedTask);

                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.AddCertificate(_organization.Username, uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Created, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void AddCertificate_WhenUserIsSubscribedToAutomaticallyOverwriteRequiredSignerPolicy_ReturnsCreated()
            {
                var uploadFile = GetUploadFile();

                _certificateService.Setup(x => x.AddCertificateAsync(
                        It.Is<HttpPostedFileBase>(file => ReferenceEquals(file, uploadFile))))
                    .ReturnsAsync(_certificate);
                _certificateService.Setup(x => x.ActivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _organization)))
                    .Returns(Task.CompletedTask);
                _certificateService.Setup(x => x.GetCertificates(
                        It.Is<User>(user => user == _organization)))
                    .Returns(new[] { _certificate });
                _securityPolicyService.Setup(x => x.IsSubscribed(
                        It.Is<User>(user => user == _organization),
                        It.Is<string>(policyName => policyName == AutomaticallyOverwriteRequiredSignerPolicy.PolicyName)))
                    .Returns(true);
                _packageService.Setup(x => x.SetRequiredSignerAsync(It.Is<User>(user => user == _user)))
                    .Returns(Task.CompletedTask);

                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.AddCertificate(_organization.Username, uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Created, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
                _securityPolicyService.VerifyAll();
                _packageService.Verify(x => x.SetRequiredSignerAsync(_organization), Times.Once);
            }

            private static StubHttpPostedFile GetUploadFile()
            {
                var bytes = Encoding.UTF8.GetBytes("certificate");
                var stream = new MemoryStream(bytes);

                return new StubHttpPostedFile((int)stream.Length, "certificate.cer", stream);
            }
        }

        public class TheDeleteCertificateAction : AccountsControllerTestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly Mock<IUserService> _userService;
            private readonly OrganizationsController _controller;
            private readonly Organization _organization;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheDeleteCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _userService = GetMock<IUserService>();
                _controller = GetController<OrganizationsController>();
                _organization = new Organization()
                {
                    Key = 1,
                    Username = "a"
                };
                _user = new User()
                {
                    Key = 2,
                    Username = "b"
                };
                _certificate = new Certificate()
                {
                    Key = 3,
                    Thumbprint = "d"
                };

                _organization.Members.Add(new Membership()
                {
                    MemberKey = _user.Key,
                    Member = _user,
                    OrganizationKey = _organization.Key,
                    Organization = _organization,
                    IsAdmin = true
                });

                _userService.Setup(x => x.FindByUsername(It.Is<string>(username => username == _organization.Username), false))
                    .Returns(_organization);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void DeleteCertificate_WhenThumbprintIsInvalid_ReturnsBadRequest(string thumbprint)
            {
                var response = _controller.DeleteCertificate(_organization.Username, thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.DeleteCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenOrganizationIsNotFound_ReturnsNotFound()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.DeleteCertificate(accountName: "nonexistent", thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.NotFound, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenCurrentUserLacksPermission_ReturnsForbidden()
            {
                var nonmember = new User()
                {
                    Key = 4,
                    Username = "e"
                };

                _controller.SetCurrentUser(nonmember);

                var response = _controller.DeleteCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenCurrentUserIsNotMultiFactorAuthenticated_ReturnsForbidden()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.DeleteCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void DeleteCertificate_WithValidThumbprint_ForAllValidClaims_ReturnsOK(bool multiFactorAuthenticatedButNotAADLoggedIn)
            {
                _certificateService.Setup(x => x.DeactivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _organization)))
                    .Returns(Task.CompletedTask);
                _controller.SetCurrentUser(_user);
                if (multiFactorAuthenticatedButNotAADLoggedIn)
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);
                }
                else
                {
                    _controller.OwinContext.AddClaim(NuGetClaims.ExternalLoginCredentialType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
                }

                var response = _controller.DeleteCertificate(_organization.Username, _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);
            }
        }

        public class TheDeleteOrganizationAccountAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DeleteHappyAccount(bool withPendingIssues)
            {
                // Arrange
                var controller = GetController<OrganizationsController>();
                var fakes = Get<Fakes>();
                var testUser = fakes.Organization;
                var username = testUser.Username;
                controller.SetCurrentUser(fakes.OrganizationAdmin);

                PackageRegistration packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(testUser);

                Package userPackage = new Package()
                {
                    Description = "TestPackage",
                    Key = 1,
                    Version = "1.0.0",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(userPackage);

                List<Package> userPackages = new List<Package>() { userPackage };

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(username, false))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                const string iconUrl = "https://icon.test/url";
                GetMock<IIconUrlProvider>()
                    .Setup(iup => iup.GetIconUrlString(It.IsAny<Package>()))
                    .Returns(iconUrl);

                // act
                var model = ResultAssert.IsView<DeleteOrganizationViewModel>(controller.Delete(accountName: username), viewName: "DeleteOrganizationAccount");

                // Assert
                Assert.Equal(username, model.AccountName);
                var package = Assert.Single(model.Packages);
                GetMock<IIconUrlProvider>()
                    .Verify(iup => iup.GetIconUrlString(It.IsAny<Package>()), Times.AtLeastOnce);
                Assert.Equal(iconUrl, package.IconUrl);
                Assert.Single(model.AdditionalMembers);
                Assert.True(model.HasAdditionalMembers);
            }
        }
    }
}