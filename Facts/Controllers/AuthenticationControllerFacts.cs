using System;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers {
    public class AuthenticationControllerFacts {
        public class The_SignIn_action {
            [Fact]
            public void will_show_the_view_with_errors_if_the_model_state_is_invalid() {
                var controller = CreateController();
                controller.ModelState.AddModelError(string.Empty, "aFakeError");

                var result = controller.SignIn(null, null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void will_sign_the_user_in_when_the_username_and_password_are_valid() {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameAndPassword("theUsername", "thePassword"))
                    .Returns(new User("theUsername", null, null));
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.SignIn(
                    new SignInRequest() { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true));
            }

            [Fact]
            public void will_invalidate_model_state_and_show_the_view_with_errors_when_the_username_and_password_are_not_valid() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var result = controller.SignIn(new SignInRequest(), "theReturnUrl") as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UserNotFound, controller.ModelState[string.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void will_redirect_to_the_return_url() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User("theUsername", null, null));
                var controller = CreateController(
                    userSvc: userSvc,
                    setup: mock => {
                        mock.Setup(x => x.SafeRedirect("theReturnUrl"))
                            .Returns(new RedirectResult("aSafeRedirectUrl"));
                    });

                var result = controller.SignIn(new SignInRequest(), "theReturnUrl") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("aSafeRedirectUrl", result.Url);
            }
        }

        public class The_SignOut_action {
            [Fact]
            public void will_sign_the_user_out() {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var controller = CreateController(formsAuthSvc: formsAuthSvc);

                controller.SignOut("theReturnUrl");

                formsAuthSvc.Verify(x => x.SignOut());
            }

            [Fact]
            public void will_redirect_to_the_return_url() {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User("theUsername", null, null));
                var controller = CreateController(
                    userSvc: userSvc,
                    setup: mock => {
                        mock.Setup(x => x.SafeRedirect("theReturnUrl"))
                            .Returns(new RedirectResult("aSafeRedirectUrl"));
                    });

                var result = controller.SignOut("theReturnUrl") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("aSafeRedirectUrl", result.Url);
            }
        }

        static AuthenticationController CreateController(
            Mock<IFormsAuthenticationService> formsAuthSvc = null,
            Mock<IUserService> userSvc = null,
            Action<Mock<AuthenticationController>> setup = null) {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            userSvc = userSvc ?? new Mock<IUserService>();

            var controller = new Mock<AuthenticationController>(
                formsAuthSvc.Object,
                userSvc.Object);

            controller.CallBase = true;

            if (setup != null)
                setup(controller);
            else {
                controller.Setup(x => x.SafeRedirect(It.IsAny<string>()))
                    .Returns(new RedirectResult("aRedirectUrl "));
            }

            return controller.Object;
        }
    }
}
