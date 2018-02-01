// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
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
            public void WhenCurrentUserIsCollaborator_ReturnsForbidden()
            {
                // Arrange
                var controller = GetController();
                var account = GetAccount(controller);
                controller.SetCurrentUser(Fakes.OrganizationCollaborator);

                // Act
                var result = InvokeAccount(controller) as HttpStatusCodeResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
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
    }
}
