// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class OrganizationsControllerFacts
        : AccountsControllerFacts<OrganizationsController, Organization, OrganizationAccountViewModel>
    {
        private static Func<Fakes, User> _getFakesUser = (Fakes fakes) => fakes.User;
        private static Func<Fakes, User> _getFakesSiteAdmin = (Fakes fakes) => fakes.Admin;
        private static Func<Fakes, User> _getFakesOrganizationAdmin = (Fakes fakes) => fakes.OrganizationAdmin;
        private static Func<Fakes, User> _getFakesOrganizationCollaborator = (Fakes fakes) => fakes.OrganizationCollaborator;

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
            public void WithNonOrganizationAdmin_ReturnsForbidden(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = InvokeConfirmationRequiredPost(controller, account, getCurrentUser) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
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

            private AddOrganizationViewModel Model = 
                new AddOrganizationViewModel { OrganizationName = OrgName, OrganizationEmailAddress = OrgEmail };

            private User Admin;

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

                var messageService = GetMock<IMessageService>();

                var controller = GetController<OrganizationsController>();
                controller.SetCurrentUser(Admin);

                var result = await controller.Add(Model);

                ResultAssert.IsRedirectToRoute(result, 
                    new { accountName = org.Username, action = nameof(OrganizationsController.ManageOrganization) });

                messageService.Verify(
                    x => x.SendNewAccountEmail(
                        org, 
                        It.Is<string>(s => s.Contains(token))), 
                    Times.Once());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationAdded(It.IsAny<Organization>()));
            }
        }

        public class TheAddMemberAction : AccountsControllerTestContainer
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

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_IsAdmin_Data))]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(Func<Fakes, User> getCurrentUser, bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeAddMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Assert.Equal(defaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);
                Assert.Equal(true, data.Pending);

                GetMock<IUserService>().Verify(s => s.AddMembershipRequestAsync(account, defaultMemberName, isAdmin), Times.Once);
                GetMock<IMessageService>()
                    .Verify(s => s.SendOrganizationMembershipRequest(
                        account,
                        It.Is<User>(u => u.Username == defaultMemberName),
                        controller.GetCurrentUser(),
                        isAdmin,
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()));
                GetMock<IMessageService>()
                    .Verify(s => s.SendOrganizationMembershipRequestInitiatedNotice(
                        account,
                        controller.GetCurrentUser(),
                        It.Is<User>(u => u.Username == defaultMemberName),
                        isAdmin,
                        It.IsAny<string>()));
            }

            private Task<JsonResult> InvokeAddMember(
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
                userService.Setup(u => u.FindByUsername(account.Username))
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
                    setup.Returns(Task.FromResult(request)).Verifiable();
                }

                // Act
                return controller.AddMember(account.Username, memberName, isAdmin);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMemberUpdatedNotice(It.IsAny<Organization>(), It.IsAny<Membership>()), Times.Never);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMemberUpdatedNotice(It.IsAny<Organization>(), It.IsAny<Membership>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeConfirmMember(controller, account, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsRedirectTo(result,
                    controller.Url.ManageMyOrganization(account.Username));


                Assert.Equal(String.Format(CultureInfo.CurrentCulture,
                    Strings.AddMember_Success, account.Username),
                    controller.TempData["Message"]);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                GetMock<IMessageService>()
                    .Verify(s => s.SendOrganizationMemberUpdatedNotice(
                        account,
                        It.Is<Membership>(m => Fakes.User.Username == m.Member.Username && m.Organization == account && m.IsAdmin == isAdmin)), Times.Once);
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
                    userService.Setup(u => u.FindByUsername(account.Username))
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

                // Act
                var result = await InvokeRejectMember(controller, account: null, isAdmin: isAdmin);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);

                GetMock<IUserService>().Verify(s => s.RejectMembershipRequestAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestRejectedNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestRejectedNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipRequestCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeRejectMember(controller, account, isAdmin: isAdmin);

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<HandleOrganizationMembershipRequestModel>(viewResult.Model);
                Assert.False(model.Confirm);
                Assert.Equal(account.Username, model.OrganizationName);
                Assert.True(model.Successful);

                GetMock<IUserService>().Verify(s => s.RejectMembershipRequestAsync(account, Fakes.User.Username, defaultConfirmationToken), Times.Once);
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestRejectedNotice(account, Fakes.User), Times.Once);
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
                    userService.Setup(u => u.FindByUsername(account.Username))
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

                // Act
                var result = await InvokeUpdateMember(controller, account, getCurrentUser, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = result.Data;
                Assert.Equal(defaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
                GetMock<IMessageService>()
                    .Verify(s => s.SendOrganizationMemberUpdatedNotice(
                        account,
                        It.Is<Membership>(m => m.Organization == account && m.Member.Username == defaultMemberName && m.IsAdmin == isAdmin)));
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
                userService.Setup(u => u.FindByUsername(account.Username))
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMemberRemovedNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMemberRemovedNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMemberRemovedNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenDeletingAsAdmin_ReturnsSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeDeleteMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.DeleteMember_Success, result.Data);

                GetMock<IUserService>()
                    .Verify(s => s.DeleteMemberAsync(account, defaultMemberName), Times.Once);
                GetMock<IMessageService>()
                    .Verify(s => s.SendOrganizationMemberRemovedNotice(account, It.Is<User>(u => u.Username == defaultMemberName)), Times.Once);
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
                userService.Setup(u => u.FindByUsername(account.Username))
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestCancelledNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
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
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestCancelledNotice(It.IsAny<Organization>(), It.IsAny<User>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(AllowedCurrentUsers_Data))]
            public async Task WhenSuccess_ReturnsSuccess(Func<Fakes, User> getCurrentUser)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeCancelMemberRequestMember(controller, account, getCurrentUser);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.CancelMemberRequest_Success, result.Data);

                GetMock<IUserService>().Verify(s => s.CancelMembershipRequestAsync(account, defaultMemberName), Times.Once);
                GetMock<IMessageService>().Verify(s => s.SendOrganizationMembershipRequestCancelledNotice(account, It.Is<User>(u => u.Username == defaultMemberName)), Times.Once);
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
                userService.Setup(u => u.FindByUsername(account.Username))
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
    }
}