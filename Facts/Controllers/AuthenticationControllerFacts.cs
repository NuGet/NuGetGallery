using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AuthenticationControllerFacts
    {
        public class TheLogOnAction
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = CreateController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.LogOn(null, null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
            }

            [Fact]
            public void CanLogTheUserOnWithUserName()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                    .Returns(new User("theUsername", null) { EmailAddress = "confirmed@example.com" });
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.LogOn(
                    new SignInRequest() { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true,
                    null));
            }

            [Fact]
            public void CanLogTheUserOnWithEmailAddress()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword("confirmed@example.com", "thePassword"))
                    .Returns(new User("theUsername", null) { EmailAddress = "confirmed@example.com" });
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.LogOn(
                    new SignInRequest() { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" },
                    "theReturnUrl");

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true,
                    null));
            }

            [Fact]
            public void WillNotLogTheUserOnWhenTheUsernameAndPasswordAreValidAndUserIsNotConfirmed()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                formsAuthSvc.Setup(x => x.SetAuthCookie(It.IsAny<string>(), It.IsAny<bool>(), null)).Throws(new InvalidOperationException());
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                    .Returns(new User("theUsername", null));
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.LogOn(
                    new SignInRequest() { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");
            }

            [Fact]
            public void WillLogTheUserOnWithRolesWhenTheUsernameAndPasswordAreValidAndUserIsConfirmed()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                    .Returns(new User("theUsername", null)
                    {
                        Roles = new[] { new Role { Name = "Administrators" } },
                        EmailAddress = "confirmed@example.com"
                    });
                var controller = CreateController(
                    formsAuthSvc: formsAuthSvc,
                    userSvc: userSvc);

                controller.LogOn(
                    new SignInRequest() { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                formsAuthSvc.Verify(x => x.SetAuthCookie(
                    "theUsername",
                    true,
                    It.Is<IEnumerable<string>>(roles => roles.Count() == 1 && roles.First() == "Administrators")));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((User)null);
                var controller = CreateController(userSvc: userSvc);

                var result = controller.LogOn(new SignInRequest(), "theReturnUrl") as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UserNotFound, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User("theUsername", null) { EmailAddress = "confirmed@example.com" });
                var controller = CreateController(
                    userSvc: userSvc,
                    setup: mock =>
                    {
                        mock.Setup(x => x.SafeRedirect("theReturnUrl"))
                            .Returns(new RedirectResult("aSafeRedirectUrl"));
                    });

                var result = controller.LogOn(new SignInRequest(), "theReturnUrl") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("aSafeRedirectUrl", result.Url);
            }
        }

        public class TheLogOffAction
        {
            [Fact]
            public void WillLogTheUserOff()
            {
                var formsAuthSvc = new Mock<IFormsAuthenticationService>();
                var controller = CreateController(formsAuthSvc: formsAuthSvc);

                controller.LogOff("theReturnUrl");

                formsAuthSvc.Verify(x => x.SignOut());
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var userSvc = new Mock<IUserService>();
                userSvc.Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new User("theUsername", null));
                var controller = CreateController(
                    userSvc: userSvc,
                    setup: mock =>
                    {
                        mock.Setup(x => x.SafeRedirect("theReturnUrl"))
                            .Returns(new RedirectResult("aSafeRedirectUrl"));
                    });

                var result = controller.LogOff("theReturnUrl") as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("aSafeRedirectUrl", result.Url);
            }
        }

        static AuthenticationController CreateController(
            Mock<IFormsAuthenticationService> formsAuthSvc = null,
            Mock<IUserService> userSvc = null,
            Action<Mock<AuthenticationController>> setup = null)
        {
            formsAuthSvc = formsAuthSvc ?? new Mock<IFormsAuthenticationService>();
            userSvc = userSvc ?? new Mock<IUserService>();

            var controller = new Mock<AuthenticationController>(
                formsAuthSvc.Object,
                userSvc.Object);

            controller.CallBase = true;

            if (setup != null)
                setup(controller);
            else
            {
                controller.Setup(x => x.SafeRedirect(It.IsAny<string>()))
                    .Returns(new RedirectResult("aRedirectUrl "));
            }

            return controller.Object;
        }
    }
}