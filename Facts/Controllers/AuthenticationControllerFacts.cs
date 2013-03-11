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

                var result = controller.LogOn(null, "/wololo");

                ResultAssert.IsView(result, viewData: new
                {
                    ReturnUrl = "/wololo"
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

                controller.MockFormsAuth
                          .Verify(x => x.SetAuthCookie(user, true));
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

                controller.MockFormsAuth
                          .Verify(x => x.SetAuthCookie(user, true));
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
                              x => x.SetAuthCookie(It.IsAny<User>(), It.IsAny<bool>()), 
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

                controller.MockFormsAuth
                          .Verify(x => x.SetAuthCookie(user, true));
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
                const string token = "foo@bar.com|John Doe|abc123|windowslive,OAuthLinkToken";
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

        public class TheLinkOrCreateUserActionOnPost {
            [Fact]
            public void WillThrowCryptoExceptionIfTokenIsInvalid()
            {
                // Arrange
                const string token = "doesn't matter,OAuthSTINKToken";
                var controller = new TestableAuthenticationController();
                
                // Act/Assert
                Assert.Throws<CryptographicException>(() => controller.LinkOrCreateUser(null, token, returnUrl: "/packages"));
            }

            [Fact]
            public void RendersViewIfModelStateHasErrors()
            {
                // Arrange
                const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                var controller = new TestableAuthenticationController();
                controller.ModelState.AddModelError(String.Empty, "EPIC FAIL!");

                // Act
                var result = controller.LinkOrCreateUser(new LinkOrCreateViewModel(), token, "/wololo");

                // Assert
                ResultAssert.IsView(result);
            }

            public class WithCreateModel
            {
                [Fact]
                public void IfUserServiceThrowsItWillAddMessageAsError()
                {
                    Assert.True(false);
                }
            }

            public class WithLinkModel
            {
                [Fact]
                public void GivenInvalidCredentialsItWillRerenderViewWithError()
                {
                    // Arrange
                    const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthLinkToken";
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

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    var viewResult = ResultAssert.IsView(result, model: model);
                    ModelStateAssert.HasErrors(
                        viewResult.ViewData.ModelState,
                        key: String.Empty,
                        errors: new ModelError(Strings.UserNotFound));
                }

                [Fact]
                public void GivenCredentialsForUnconfirmedAccountItRerendersTheViewWithTheConfirmationRequiredFlag()
                {
                    // Arrange
                    const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthLinkToken";
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
                              .Returns(new User() { EmailAddress = null, UnconfirmedEmailAddress = "foo@bar.com" });

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    var viewResult = ResultAssert.IsView(result, model: model, viewData: new
                    {
                        ConfirmationRequired = true
                    });
                }

                [Fact]
                public void GivenCredentialsForValidAccountItSavesCredentialAndLogsUserIn()
                {
                    // Arrange
                    const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                        {
                            UserNameOrEmail = "foo@bar.com",
                            Password = "hunter2"
                        }
                    };
                    var user = new User() { EmailAddress = "foo@bar.com" };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.FindByUsernameAndPassword("foo@bar.com", "hunter2"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(true);

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    controller.MockUsers
                              .Verify(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"));
                    controller.MockFormsAuth
                              .Verify(f => f.SetAuthCookie(user, true));
                    ResultAssert.IsRedirectTo(result, "aSafeRedirectUrl");
                }

                [Fact]
                public void GivenDuplicateCredentialItReportsAnErrorToTheUser()
                {
                    // Arrange
                    const string token = "foo@bar.com|Andrew Stanton-Nurse|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        LinkModel = new LinkOrCreateViewModel.LinkViewModel()
                        {
                            UserNameOrEmail = "foo@bar.com",
                            Password = "hunter2"
                        }
                    };
                    var user = new User() { EmailAddress = "foo@bar.com" };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.FindByUsernameAndPassword("foo@bar.com", "hunter2"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(false);

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    controller.MockUsers
                              .Verify(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"));
                    controller.MockFormsAuth
                              .Verify(f => f.SetAuthCookie(user, true), Times.Never());
                    var viewResult = ResultAssert.IsView(result, model: model);
                    ModelStateAssert.HasErrors(viewResult.ViewData.ModelState, String.Empty, Strings.DuplicateOAuthCredential);
                }
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
