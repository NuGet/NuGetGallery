using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Moq;
using Xunit;
using System.Net.Mail;
using NuGetGallery.Framework;
using NuGetGallery.Authentication;
using Microsoft.Owin;
using System.Threading.Tasks;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery.Controllers
{
    public class AuthenticationControllerFacts
    {
        public class TheLogOffAction : TestContainer
        {
            [Fact]
            public void WillLogTheUserOff()
            {
                var controller = GetController<AuthenticationController>();
                
                controller.LogOff("theReturnUrl");

                var revoke = controller.OwinContext.Authentication.AuthenticationResponseRevoke;
                Assert.NotNull(revoke);
                Assert.Empty(revoke.AuthenticationTypes);
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var controller = GetController<AuthenticationController>();
                
                var result = controller.LogOff("theReturnUrl");
                ResultAssert.IsRedirectTo(result, "/");
            }
        }

        public class TheSignInAction : TestContainer
        {
            [Fact]
            public async Task WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = GetController<AuthenticationController>();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = await controller.SignIn(new LogOnViewModel(), null, linkingAccount: false);

                ResultAssert.IsView(result, viewName: "LogOn", viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public async Task CanLogTheUserOnWithUserName()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .ReturnsAsync(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                
                // Act
                await controller.SignIn(
                    new LogOnViewModel()
                    {
                        SignIn = new SignInViewModel
                        {
                            UserNameOrEmail = authUser.User.Username,
                            Password = "thePassword"
                        }
                    },
                    "theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public async Task CanLogTheUserOnWithEmailAddress()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .ReturnsAsync(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                
                // Act
                await controller.SignIn(
                    new LogOnViewModel()
                    {
                        SignIn = new SignInViewModel
                        {
                            UserNameOrEmail = "confirmed@example.com",
                            Password = "thePassword"
                        }
                    },
                    "theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public async Task WillLogTheUserOnWithUsernameEvenWithoutConfirmedEmailAddress()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .ReturnsAsync(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                
                // Act
                await controller.SignIn(
                    new LogOnViewModel()
                    {
                        SignIn = new SignInViewModel
                        {
                            UserNameOrEmail = "confirmed@example.com",
                            Password = "thePassword"
                        }
                    },
                    "theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public async Task WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsNull();
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignIn(
                    new LogOnViewModel() { SignIn = new SignInViewModel() },
                    "theReturnUrl", linkingAccount: false);

                ResultAssert.IsView(result, viewName: "LogOn");
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UsernameAndPasswordNotFound, controller.ModelState["SignIn"].Errors[0].ErrorMessage);
            }
            
            [Fact]
            public async Task WillRedirectToHomeIfReturnUrlNotLocal()
            {
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .ReturnsAsync(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User));
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignIn(
                    new LogOnViewModel()
                    {
                        SignIn = new SignInViewModel
                        {
                            UserNameOrEmail = "confirmed@example.com",
                            Password = "thePassword"
                        }
                    },
                    "http://www.microsoft.com", linkingAccount: false);

                ResultAssert.IsRedirectTo(result, "/");
            }
            
            [Fact]
            public async Task WillRedirectToTheReturnUrlIfLocal()
            {
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .ReturnsAsync(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User));
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignIn(
                    new LogOnViewModel()
                    {
                        SignIn = new SignInViewModel
                        {
                            UserNameOrEmail = "confirmed@example.com",
                            Password = "thePassword"
                        }
                    },
                    "/packages/upload", linkingAccount: false);

                ResultAssert.IsRedirectTo(result, "/packages/upload");
            }
        }

        public class TheRegisterAction : TestContainer
        {
            [Fact]
            public async Task WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = GetController<AuthenticationController>();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = await controller.Register(new LogOnViewModel(), null, linkingAccount: false);

                ResultAssert.IsView(result, viewName: "LogOn", viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public async Task WillCreateAndLogInTheUser()
            {
                var authUser = new AuthenticatedUser(new User("theUsername"), new Credential());
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "theEmailAddress", It.IsAny<Credential>()))
                    .ReturnsAsync(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User))
                    .Verifiable();
                var controller = GetController<AuthenticationController>();

                await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            Password = "thePassword",
                            EmailAddress = "theEmailAddress",
                        }
                    }, null, linkingAccount: false);

                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public async Task WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Credential>()))
                    .Throws(new EntityException("aMessage"));
                var controller = GetController<AuthenticationController>();

                var request = new LogOnViewModel()
                {
                    Register = new RegisterViewModel
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "theEmailAddress",
                    }
                };
                var result = await controller.Register(request, null, linkingAccount: false);

                ResultAssert.IsView(result, viewName: "LogOn");
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState["Register"].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillRedirectToTheReturnUrl()
            {
                var user = new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" };
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "unconfirmed@example.com", It.IsAny<Credential>()))
                    .ReturnsAsync(new AuthenticatedUser(user, new Credential()));
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), user));
                var controller = GetController<AuthenticationController>();

                var result = await controller.Register(new LogOnViewModel()
                {
                    Register = new RegisterViewModel
                    {
                        EmailAddress = "unconfirmed@example.com",
                        Password = "thepassword",
                        Username = "theUsername",
                    }
                }, "/theReturnUrl", linkingAccount: false);

                ResultAssert.IsRedirectTo(result, "/theReturnUrl");
            }
        }
    }
}

