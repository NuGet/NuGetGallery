using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Moq;
using Xunit;
using System.Net.Mail;

namespace NuGetGallery.Controllers
{
    public class AuthenticationControllerFacts
    {
        public class TheLogOffAction
        {
            [Fact]
            public void WillLogTheUserOff()
            {
                var controller = new TestableAuthenticationController();

                controller.LogOff("theReturnUrl");

                controller.MockFormsAuth.Verify(x => x.SignOut());
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(new User("theUsername", null));
                

                var result = controller.LogOff("theReturnUrl") as RedirectResult;
                ResultAssert.IsRedirectTo(result, "aSafeRedirectUrl");
            }
        }

        public class TheSignInAction
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = new TestableAuthenticationController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.SignIn(null, null);

                ResultAssert.IsView(result, viewData: new
                {
                    ReturnUrl = (string)null
                });
            }


            [Fact]
            public void CanLogTheUserOnWithUserName()
            {
                var controller = new TestableAuthenticationController();
                var user = new User("theUsername", null) { EmailAddress = "confirmed@example.com" };
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                          .Returns(user);

                controller.SignIn(
                    new SignInRequest { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth.Verify(
                    x => x.SetAuthCookie(
                        "theUsername",
                        true,
                        null));
            }

            [Fact]
            public void CanLogTheUserOnWithEmailAddress()
            {
                var controller = new TestableAuthenticationController();
                var user = new User("theUsername", null) { EmailAddress = "confirmed@example.com" };
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword("confirmed@example.com", "thePassword"))
                          .Returns(user);

                controller.SignIn(
                    new SignInRequest { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth.Verify(
                    x => x.SetAuthCookie(
                        "theUsername",
                        true,
                        null));
            }

            [Fact]
            public void WillLogTheUserOnWithUsernameEvenWithoutConfirmedEmailAddress()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                          .Returns(new User("theUsername", null));

                controller.SignIn(
                    new SignInRequest { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth
                          .Verify(
                              x => x.SetAuthCookie(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()),
                              Times.Never());
            }

            [Fact]
            public void WillLogTheUserOnWithRoles()
            {
                var controller = new TestableAuthenticationController();
                var user = new User("theUsername", null)
                {
                    Roles = new[] { new Role { Name = "Administrators" } },
                    EmailAddress = "confirmed@example.com"
                };
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                          .Returns(user);

                controller.SignIn(
                    new SignInRequest { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth.Verify(
                    x => x.SetAuthCookie(
                        "theUsername",
                        true,
                        new[] { "Administrators" }));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .ReturnsNull();

                var result = controller.SignIn(new SignInRequest(), "theReturnUrl") as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UserNotFound, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }
            
            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(new User("theUsername", null) { EmailAddress = "confirmed@example.com" });

                var result = controller.SignIn(new SignInRequest(), "theReturnUrl");

                ResultAssert.IsRedirectTo(result, "aSafeRedirectUrl");
            }
        }

        public class TheRegisterAction
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = new TestableAuthenticationController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.Register(null, null);

                ResultAssert.IsView(result, viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public void WillCreateTheUser()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(new User { Username = "theUsername", EmailAddress = "to@example.com" });

                controller.Register(
                    new RegisterRequest
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "theEmailAddress",
                    }, null);

                controller.MockUsers
                            .Verify(x => x.Create("theUsername", "thePassword", "theEmailAddress"));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                            .Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Throws(new EntityException("aMessage"));

                var result = controller.Register(
                    new RegisterRequest
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "theEmailAddress",
                    }, null) as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .Returns(new User("theUsername", null) { EmailAddress = "confirmed@example.com" });

                var result = controller.Register(new RegisterRequest(), "theReturnUrl");

                ResultAssert.IsRedirectTo(result, "aSafeRedirectUrl");
            }
        }

        public class TestableAuthenticationController : AuthenticationController
        {
            public Mock<IFormsAuthenticationService> MockFormsAuth { get; private set; }
            public Mock<IUserService> MockUsers { get; private set; }

            public TestableAuthenticationController()
            {
                FormsAuth = (MockFormsAuth = new Mock<IFormsAuthenticationService>()).Object;
                UserService = (MockUsers = new Mock<IUserService>()).Object;
            }

            protected override ActionResult SafeRedirect(string returnUrl)
            {
                return new RedirectResult("aSafeRedirectUrl");
            }
        }
    }
}