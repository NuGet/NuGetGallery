using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Moq;
using Xunit;
using System.Net.Mail;
using NuGetGallery.Framework;
using NuGetGallery.Authentication;
using Microsoft.Owin;

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
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = GetController<AuthenticationController>();
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
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .Returns(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User, AuthenticationTypes.LocalUser))
                    .Verifiable();
                
                // Act
                controller.SignIn(
                    new SignInViewModel { UserNameOrEmail = authUser.User.Username, Password = "thePassword" },
                    "theReturnUrl");

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public void CanLogTheUserOnWithEmailAddress()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .Returns(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User, AuthenticationTypes.LocalUser))
                    .Verifiable();
                
                // Act
                controller.SignIn(
                    new SignInViewModel { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" },
                    "theReturnUrl");

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public void WillLogTheUserOnWithUsernameEvenWithoutConfirmedEmailAddress()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .Returns(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User, AuthenticationTypes.LocalUser))
                    .Verifiable();
                
                // Act
                controller.SignIn(
                    new SignInViewModel { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" },
                    "theReturnUrl");

                // Assert
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsNull();
                var controller = GetController<AuthenticationController>();
                
                var result = controller.SignIn(new SignInViewModel(), "theReturnUrl") as ViewResult;

                Assert.NotNull(result);
                Assert.Empty(result.ViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UsernameAndPasswordNotFound, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }
            
            [Fact]
            public void WillRedirectToHomeIfReturnUrlNotLocal()
            {
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .Returns(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User, AuthenticationTypes.LocalUser));
                var controller = GetController<AuthenticationController>();
                
                var result = controller.SignIn(
                    new SignInViewModel { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" }, 
                    "http://www.microsoft.com");

                ResultAssert.IsRedirectTo(result, "/");
            }
            
            [Fact]
            public void WillRedirectToTheReturnUrlIfLocal()
            {
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" },
                    new Credential() { Type = "Foo" });
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .Returns(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User, AuthenticationTypes.LocalUser));
                var controller = GetController<AuthenticationController>();
                
                var result = controller.SignIn(
                    new SignInViewModel { UserNameOrEmail = "confirmed@example.com", Password = "thePassword" }, 
                    "/packages/upload");

                ResultAssert.IsRedirectTo(result, "/packages/upload");
            }
        }

        public class TheRegisterAction : TestContainer
        {
            [Fact]
            public void WillShowTheViewWithErrorsIfTheModelStateIsInvalid()
            {
                var controller = GetController<AuthenticationController>();
                controller.ModelState.AddModelError(String.Empty, "aFakeError");

                var result = controller.Register(null, null);

                ResultAssert.IsView(result, viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public void WillCreateAndLogInTheUser()
            {
                var authUser = new AuthenticatedUser(new User("theUsername"), new Credential());
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "thePassword", "theEmailAddress"))
                    .Returns(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), authUser.User, AuthenticationTypes.LocalUser))
                    .Verifiable();
                var controller = GetController<AuthenticationController>();
                
                controller.Register(
                    new RegisterViewModel
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "theEmailAddress",
                    }, null);

                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public void WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Throws(new EntityException("aMessage"));
                var controller = GetController<AuthenticationController>();
                
                var request = new RegisterViewModel
                {
                    Username = "theUsername",
                    Password = "thePassword",
                    EmailAddress = "theEmailAddress",
                };
                var result = controller.Register(request, null);

                ResultAssert.IsView(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public void WillRedirectToTheReturnUrl()
            {
                var user = new User("theUsername") { UnconfirmedEmailAddress = "unconfirmed@example.com" };
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "thepassword", "unconfirmed@example.com"))
                    .Returns(new AuthenticatedUser(user, new Credential()));
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(It.IsAny<IOwinContext>(), user, AuthenticationTypes.LocalUser));
                var controller = GetController<AuthenticationController>();
                
                var result = controller.Register(new RegisterViewModel
                    {
                        EmailAddress = "unconfirmed@example.com",
                        Password = "thepassword",
                        Username = "theUsername",
                    }, "/theReturnUrl");

                ResultAssert.IsRedirectTo(result, "/theReturnUrl");
            }
        }
    }
}

