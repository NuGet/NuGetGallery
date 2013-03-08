using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Mvc;
using Moq;
using Xunit;

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

        public class TheLogOnAction
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = new TestableAuthenticationController();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.LogOn(null, null);

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
                
                controller.LogOn(
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
                
                controller.LogOn(
                    new SignInRequest { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth.Verify(
                    x => x.SetAuthCookie(
                        "theUsername",
                        true,
                        null));
            }

            [Fact]
            public void WillNotLogTheUserOnWhenTheUsernameAndPasswordAreValidAndUserIsNotConfirmed()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword("theUsername", "thePassword"))
                          .Returns(new User("theUsername", null));
                
                controller.LogOn(
                    new SignInRequest { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth
                          .Verify(
                              x => x.SetAuthCookie(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>()), 
                              Times.Never());
            }

            [Fact]
            public void WillLogTheUserOnWithRolesWhenTheUsernameAndPasswordAreValidAndUserIsConfirmed()
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
                
                controller.LogOn(
                    new SignInRequest { UserNameOrEmail = "theUsername", Password = "thePassword" },
                    "theReturnUrl");

                controller.MockFormsAuth.Verify(
                    x => x.SetAuthCookie(
                        "theUsername",
                        true,
                        new [] { "Administrators" }));
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(x => x.FindByUsernameOrEmailAddressAndPassword(It.IsAny<string>(), It.IsAny<string>()))
                          .ReturnsNull();

                var result = controller.LogOn(new SignInRequest(), "theReturnUrl") as ViewResult;

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
                
                var result = controller.LogOn(new SignInRequest(), "theReturnUrl");

                ResultAssert.IsRedirectTo(result, "aSafeRedirectUrl");
            }
        }

        public class TheLinkOrCreateUserActionOnGet
        {
            [Fact]
            public void WillPrefillFieldsIfSpecifiedOnGet()
            {
                // Arrange
                const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                var controller = new TestableAuthenticationController();

                // Act
                var result = controller.LinkOrCreateUser(token, returnUrl: "/packages");

                // Assert
                ResultAssert.IsView(result, model: new LinkOrCreateViewModel()
                {
                    LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                    {
                        UserNameOrEmail = "foo@bar.com"
                    },
                    CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                    {
                        EmailAddress = "foo@bar.com",
                        Username = "foobar"
                    }
                }, viewData: new
                {
                    ReturnUrl = "/packages" // ReturnUrl is specified in ViewData to handle the Log On link in the layout
                });
            }

            [Fact]
            public void WillNotPrefillUsernameIfDoesNotMatchRegex()
            {
                // Arrange
                const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthLinkToken";
                var controller = new TestableAuthenticationController();

                // Act
                var result = controller.LinkOrCreateUser(token, returnUrl: "/packages");

                // Assert
                ResultAssert.IsView(result, model: new LinkOrCreateViewModel()
                {
                    LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                    {
                        UserNameOrEmail = "foo@bar.com"
                    },
                    CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                    {
                        EmailAddress = "foo@bar.com",
                        Username = null
                    }
                }, viewData: new
                {
                    ReturnUrl = "/packages" // ReturnUrl is specified in ViewData to handle the Log On link in the layout
                });
            }
        }

        public class TheLinkOrCreateUserActionOnPost
        {
            [Fact]
            public void WillThrowCryptoExceptionIfTokenIsInvalid()
            {
                // Arrange
                const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthSTINKToken";
                var controller = new TestableAuthenticationController();
                
                // Act/Assert
                Assert.Throws<CryptographicException>(() => controller.LinkOrCreateUser(null, token, returnUrl: "/packages"));
            }

            [Fact]
            public void GivenALinkTokenWithInvalidCredentialsItWillRerenderViewWithError()
            {
                // Arrange
                const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthSTINKToken";
                var model = new LinkOrCreateViewModel()
                {
                    LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                    {
                        UserNameOrEmail = "foo@bar.com",
                        Password = "nodice"
                    }
                };
                var controller = new TestableAuthenticationController();
                controller.MockUsers
                          .Setup(u => u.FindByUsernameAndPassword("foo@bar.com", "nodice"))
                          .ReturnsNull();
            }

        }

        public class TestableAuthenticationController : AuthenticationController
        {
            public Mock<IFormsAuthenticationService> MockFormsAuth { get; private set; }
            public Mock<IUserService> MockUsers { get; private set; }

            public TestableAuthenticationController()
            {
                FormsAuth = (MockFormsAuth = new Mock<IFormsAuthenticationService>()).Object;
                Users = (MockUsers = new Mock<IUserService>()).Object;
                Crypto = new TestCryptoService();
            }

            public override ActionResult SafeRedirect(string returnUrl)
            {
                return new RedirectResult("aSafeRedirectUrl");
            }
        }
    }
}
