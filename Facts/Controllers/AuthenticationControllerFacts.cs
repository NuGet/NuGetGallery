using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Web;
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
                var userService = new Mock<IUserService>();
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

        public class TheLinkOrCreateUserActionOnPost
        {
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
                var model = new LinkOrCreateViewModel();

                // Act
                var result = controller.LinkOrCreateUser(model, token, "/wololo");

                // Assert
                ResultAssert.IsView(result, model: model, viewData: new
                {
                    ReturnUrl = "/wololo"
                });
            }

            [Fact]
            public void WillReturnGetViewIfNeitherModelSpecified()
            {
                // Arrange
                const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                var controller = new TestableAuthenticationController();
                
                // Act
                var result = controller.LinkOrCreateUser(new LinkOrCreateViewModel(), token, "/wololo");

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
                    ReturnUrl = "/wololo" // ReturnUrl is specified in ViewData to handle the Log On link in the layout
                });
            }

            public class WithCreateModel
            {
                [Fact]
                public void IfUserServiceThrowsEntityExceptionItWillAddMessageAsError()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel() {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel() {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.Create("foobar", "hunter2", "foo@bar.com"))
                              .Throws(new EntityException("Oh noes!"));

                    // Assert
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Act
                    var viewResult = ResultAssert.IsView(result, model: model, viewData: new
                    {
                        ReturnUrl = "/wololo"
                    });
                    ModelStateAssert.HasErrors(viewResult.ViewData.ModelState, String.Empty, "Oh noes!");
                }

                [Fact]
                public void IfUserServiceReturnsNullThrowsInvalidOperationException()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                        {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var controller = new TestableAuthenticationController();

                    // Assert
                    Assert.Throws<InvalidOperationException>(() => controller.LinkOrCreateUser(model, token, "/wololo"));
                }

                [Fact]
                public void IfUserCreatedItAssociatesCredential()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                        {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var user = new User()
                    {
                         Username = "foobar",
                         EmailAddress = "foo@bar.com"
                    };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.Create("foobar", "hunter2", "foo@bar.com"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(true);

                    // Assert
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Act
                    ResultAssert.IsRedirectToRoute(result, new
                    {
                        Controller = "Users",
                        Action = "Thanks"
                    });
                    controller.MockUsers
                              .Verify(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"));
                }

                [Fact]
                public void IfAssociateCredentialReturnsFalseItThrowsInvalidOperationException()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                        {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var user = new User()
                    {
                        Username = "foobar",
                        EmailAddress = "foo@bar.com"
                    };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.Create("foobar", "hunter2", "foo@bar.com"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(false);

                    // Assert
                    Assert.Throws<InvalidOperationException>(() => controller.LinkOrCreateUser(model, token, "/wololo"));
                }

                [Fact]
                public void IfConfirmEmailAddressIsOffItRedirectsToThanks()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                        {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var user = new User()
                    {
                        Username = "foobar",
                        EmailAddress = "foo@bar.com"
                    };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.Create("foobar", "hunter2", "foo@bar.com"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(true);
                    
                    // Assert
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Act
                    ResultAssert.IsRedirectToRoute(result, new {
                        Controller = "Users",
                        Action = "Thanks"
                    });
                    controller.MockUsers
                              .Verify(u => u.Create("foobar", "hunter2", "foo@bar.com"));
                }

                [Fact]
                public void IfConfirmEmailAddressIsOnSendsConfirmationMailAndRedirectsToThanks()
                {
                    // Arrange
                    const string token = "foo@bar.com|foobar|abc123|windowslive,OAuthLinkToken";
                    var model = new LinkOrCreateViewModel()
                    {
                        CreateModel = new LinkOrCreateViewModel.CreateViewModel()
                        {
                            Username = "foobar",
                            EmailAddress = "foo@bar.com",
                            Password = "hunter2",
                            ConfirmPassword = "hunter2"
                        }
                    };
                    var user = new User()
                    {
                        Username = "foobar",
                        EmailAddress = "foo@bar.com"
                    };
                    var controller = new TestableAuthenticationController();
                    controller.MockUsers
                              .Setup(u => u.Create("foobar", "hunter2", "foo@bar.com"))
                              .Returns(user);
                    controller.MockUsers
                              .Setup(u => u.AssociateCredential(user, "oauth:windowslive", "abc123"))
                              .Returns(true);
                    controller.MockConfig
                              .Setup(c => c.ConfirmEmailAddresses)
                              .Returns(true);
                    

                    // Assert
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Act
                    ResultAssert.IsRedirectToRoute(result, new
                    {
                        Controller = "Users",
                        Action = "Thanks"
                    });
                    controller.MockUsers
                              .Verify(u => u.Create("foobar", "hunter2", "foo@bar.com"));
                    controller.MockMessages
                              .Verify(m => m.SendNewAccountEmail(new MailAddress("foo@bar.com", "foobar"), "https://example.org/?Controller=Users&Action=Confirm&username=foobar"));
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
                              .Setup(u => u.FindByUsernameOrEmailAddressAndPassword("foo@bar.com", "nodice"))
                              .ReturnsNull();

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    var viewResult = ResultAssert.IsView(result, model: model, viewData: new
                    {
                        ReturnUrl = "/wololo"
                    });
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
                              .Setup(u => u.FindByUsernameOrEmailAddressAndPassword("foo@bar.com", "nodice"))
                              .Returns(new User() { EmailAddress = null, UnconfirmedEmailAddress = "foo@bar.com" });

                    // Act
                    var result = controller.LinkOrCreateUser(model, token, returnUrl: "/wololo");

                    // Assert
                    var viewResult = ResultAssert.IsView(result, model: model, viewData: new
                    {
                        ConfirmationRequired = true,
                        ReturnUrl = "/wololo"
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
                              .Setup(u => u.FindByUsernameOrEmailAddressAndPassword("foo@bar.com", "hunter2"))
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
                              .Setup(u => u.FindByUsernameOrEmailAddressAndPassword("foo@bar.com", "hunter2"))
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
                    var viewResult = ResultAssert.IsView(result, model: model, viewData: new
                    {
                        ReturnUrl = "/wololo"
                    });
                    ModelStateAssert.HasErrors(viewResult.ViewData.ModelState, String.Empty, Strings.DuplicateOAuthCredential);
                }
            }
        }

        public class TestableAuthenticationController : AuthenticationController
        {
            public Mock<IFormsAuthenticationService> MockFormsAuth { get; private set; }
            public Mock<IUserService> MockUsers { get; private set; }
            public Mock<IConfiguration> MockConfig { get; private set; }
            public Mock<IMessageService> MockMessages { get; private set; }
            
            public TestableAuthenticationController()
            {
                FormsAuth = (MockFormsAuth = new Mock<IFormsAuthenticationService>()).Object;
                Users = (MockUsers = new Mock<IUserService>()).Object;
                Config = (MockConfig = new Mock<IConfiguration>()).Object;
                Messages = (MockMessages = new Mock<IMessageService>()).Object;
                Crypto = new TestCryptoService();

                TestUtility.SetupHttpContextMockForUrlGeneration(new Mock<HttpContextBase>(), this);
            }

            public override ActionResult SafeRedirect(string returnUrl)
            {
                return new RedirectResult("aSafeRedirectUrl");
            }
        }
    }
}