// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        public class TheAccountAction : TheAccountBaseAction
        {
            protected override ActionResult InvokeAccount(OrganizationsController controller)
            {
                var accountName = GetAccount(controller).Username;
                return controller.ManageOrganization(accountName);
            }

            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public void WhenCurrentUserIsCollaborator_ReturnsReadOnly()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = InvokeAccount(controller);

                // Assert
                var model = ResultAssert.IsView<OrganizationAccountViewModel>(result, "ManageOrganization");
                Assert.False(model.CanManage);
            }

            [Fact]
            public void WhenCurrentUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = InvokeAccount(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheCancelChangeEmailAction : TheCancelChangeEmailBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public async Task WhenCurrentUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeCancelChangeEmail(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeCancelChangeEmail(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheChangeEmailAction : TheChangeEmailBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public async Task WhenCurrentUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeChangeEmail(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeChangeEmail(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheChangeEmailSubscriptionAction : TheChangeEmailSubscriptionBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public async Task WhenCurrentUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeChangeEmailSubscription(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public async Task WhenCurrentUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeChangeEmailSubscription(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmationRequiredAction : TheConfirmationRequiredBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public void WhenUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = InvokeConfirmationRequired(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void WhenUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = InvokeConfirmationRequired(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmationRequiredPostAction : TheConfirmationRequiredPostBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public void WhenUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = InvokeConfirmationRequiredPost(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }

            [Fact]
            public void WhenUserIsNotMember_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = InvokeConfirmationRequiredPost(controller, account) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
            }
        }

        public class TheConfirmAction : TheConfirmBaseAction
        {
            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            // Note general account tests are in the base class. Organization-specific tests are below.

            [Fact]
            public async Task WhenUserIsCollaborator_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeConfirm(controller, account);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.WrongUsername);
                Assert.False(model.SuccessfulConfirmation);
            }

            [Fact]
            public async Task WhenUserIsNotMember_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeConfirm(controller, account);

                // Assert
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.WrongUsername);
                Assert.False(model.SuccessfulConfirmation);
            }
        }

        public class TheCreateAction : TestContainer
        {
            private const string OrgName = "TestOrg";
            private const string OrgEmail = "TestOrg@testorg.com";

            private AddOrganizationViewModel Model = 
                new AddOrganizationViewModel { OrganizationName = OrgName, OrganizationEmailAddress = OrgEmail };

            private User Admin;

            private Fakes Fakes;

            public TheCreateAction()
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
                Assert.Equal(message, controller.TempData["ErrorMessage"]);
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
                        It.Is<MailAddress>(m => m.Address == OrgEmail && m.DisplayName == org.Username), 
                        It.Is<string>(s => s.Contains(token))), 
                    Times.Once());
            }
        }

        public class TheAddMemberAction : AccountsControllerTestContainer
        {
            private const string defaultMemberName = "member";

            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            [Fact]
            public async Task WhenUserIsCollaborator_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeAddMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task WhenUserIsNotMember_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeAddMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenEntityException_ReturnsNonSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeAddMember(controller, account, isAdmin: isAdmin,
                    exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeAddMember(controller, account, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = ((JsonResult)result).Data;
                Assert.Equal(defaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);

                GetMock<IUserService>().Verify(s => s.AddMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            private Task<JsonResult> InvokeAddMember(
                OrganizationsController controller,
                Organization account,
                string memberName = defaultMemberName,
                bool isAdmin = false,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.AddMemberAsync(It.IsAny<Organization>(), memberName, isAdmin));
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
                return controller.AddMember(account.Username, memberName, isAdmin);
            }
        }

        public class TheUpdateMemberAction : AccountsControllerTestContainer
        {
            private const string defaultMemberName = "member";

            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            [Fact]
            public async Task WhenUserIsCollaborator_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeUpdateMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public async Task WhenUserIsNotMember_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeUpdateMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(It.IsAny<Organization>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenEntityException_ReturnsNonSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeUpdateMember(controller, account, isAdmin: isAdmin,
                    exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenMembershipCreated_ReturnsSuccess(bool isAdmin)
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeUpdateMember(controller, account, isAdmin: isAdmin);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);

                dynamic data = ((JsonResult)result).Data;
                Assert.Equal(defaultMemberName, data.Username);
                Assert.Equal(isAdmin, data.IsAdmin);

                GetMock<IUserService>().Verify(s => s.UpdateMemberAsync(account, defaultMemberName, isAdmin), Times.Once);
            }

            private Task<JsonResult> InvokeUpdateMember(
                OrganizationsController controller,
                Organization account,
                string memberName = defaultMemberName,
                bool isAdmin = false,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

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

            protected override User GetCurrentUser(OrganizationsController controller)
            {
                return controller.GetCurrentUser() ?? Fakes.OrganizationAdmin;
            }

            [Fact]
            public async Task WhenUserIsCollaborator_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = await InvokeDeleteMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(It.IsAny<Organization>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task WhenUserIsNotMember_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await InvokeDeleteMember(controller, account);

                // Assert
                Assert.Equal((int)HttpStatusCode.Forbidden, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.Unauthorized, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(It.IsAny<Organization>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task WhenEntityException_ReturnsNonSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeDeleteMember(controller, account, exception: new EntityException("error"));

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal("error", ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(account, defaultMemberName), Times.Once);
            }

            [Fact]
            public async Task WhenDeletingAsAdmin_ReturnsSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);

                // Act
                var result = await InvokeDeleteMember(controller, account);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.DeleteMember_Success, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(account, defaultMemberName), Times.Once);
            }

            [Fact]
            public async Task WhenDeletingSelfAsCollaborator_ReturnsSuccess()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                var collaborator = Fakes.OrganizationCollaborator;
                controller.SetCurrentUser(collaborator);

                // Act
                var result = await InvokeDeleteMember(controller, account, memberName: collaborator.Username);

                // Assert
                Assert.Equal(0, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.Equal(Strings.DeleteMember_Success, ((JsonResult)result).Data);

                GetMock<IUserService>().Verify(s => s.DeleteMemberAsync(account, collaborator.Username), Times.Once);
            }

            private Task<JsonResult> InvokeDeleteMember(
                OrganizationsController controller,
                Organization account,
                string memberName = defaultMemberName,
                EntityException exception = null)
            {
                // Arrange
                controller.SetCurrentUser(GetCurrentUser(controller));

                var userService = GetMock<IUserService>();
                userService.Setup(u => u.FindByUsername(account.Username))
                    .Returns(account as User);
                var setup = userService.Setup(u => u.DeleteMemberAsync(It.IsAny<Organization>(), memberName));
                if (exception != null)
                {
                    setup.Throws(exception);
                }
                else
                {
                    setup.Returns(Task.CompletedTask).Verifiable();
                }

                // Act
                return controller.DeleteMember(account.Username, memberName);
            }
        }
    }
}
