// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
using NuGetGallery.Configuration;
using System.Security.Claims;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;

namespace NuGetGallery.Controllers
{
    public class AuthenticationControllerFacts
    {
        public class TheLogOnAction : TestContainer
        {
            [Fact]
            public void GivenUserAlreadyAuthenticated_ItRedirectsToReturnUrl()
            {
                // Arrange
                var controller = GetController<AuthenticationController>();
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = controller.LogOn("/foo/bar/baz");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "/foo/bar/baz");
                Assert.Equal(Strings.AlreadyLoggedIn, controller.TempData["Message"]);
            }

            [Fact]
            public void GivenNoAuthenticatedUser_ItLoadsProvidersIntoViewModelAndDisplaysLogOnView()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();
                
                // Act
                var result = controller.LogOn("/foo");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: "LogOn");
                Assert.NotNull(model.SignIn);
                Assert.NotNull(model.Register);
                Assert.Equal(1, model.Providers.Count);
                Assert.Equal("MicrosoftAccount", model.Providers[0].ProviderName);
            }
        }

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
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
            }
        }

        public class TheSignInAction : TestContainer
        {
            [Fact]
            public async Task GivenUserAlreadyAuthenticated_ItRedirectsToReturnUrl()
            {
                // Arrange
                var controller = GetController<AuthenticationController>();
                controller.SetCurrentUser(Fakes.User);

                // Act
                var result = await controller.SignIn(new LogOnViewModel(), "/foo/bar/baz", linkingAccount: false);

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "/foo/bar/baz");
                Assert.Equal(Strings.AlreadyLoggedIn, controller.TempData["Message"]);
            }

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
            public async Task WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWithNull();
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignIn(
                    new LogOnViewModel() { SignIn = new SignInViewModel() },
                    "theReturnUrl", linkingAccount: false);

                ResultAssert.IsView(result, viewName: "LogOn");
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UsernameAndPasswordNotFound, controller.ModelState["SignIn"].Errors[0].ErrorMessage);
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
                    .CompletesWith(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            authUser.User.Username, 
                            "thePassword")),
                    "theReturnUrl", linkingAccount: false);

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
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
                    .CompletesWith(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            "confirmed@example.com", 
                            "thePassword")),
                    "theReturnUrl", linkingAccount: false);

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
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
                    .CompletesWith(authUser);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            "confirmed@example.com", 
                            "thePassword")),
                    "theReturnUrl", linkingAccount: false);

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                GetMock<AuthenticationService>().VerifyAll();
            }

            [Fact]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authUser);

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            authUser.User.Username, 
                            "thePassword")),
                    "theReturnUrl", linkingAccount: true);

                // Assert
                VerifyExternalLinkExpiredResult(controller, result);
                GetMock<AuthenticationService>()
                    .Verify(x => x.CreateSession(It.IsAny<IOwinContext>(), It.IsAny<User>()), Times.Never());
            }

            [Fact]
            public async Task GivenValidExternalAuth_ItLinksCredentialSendsEmailAndLogsIn()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential() { Type = "Foo" });
                var externalCred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.AddCredential(authUser.User, externalCred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(x => x.SendCredentialAddedNotice(authUser.User, externalCred))
                    .Verifiable();

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred
                    });

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            authUser.User.Username,
                            "thePassword")),
                    "theReturnUrl", linkingAccount: true);

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
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
            public async Task WillCreateAndLogInTheUserWhenNotLinking()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { 
                        UnconfirmedEmailAddress = "unconfirmed@example.com", 
                        EmailConfirmationToken = "t0k3n" 
                    }, 
                    new Credential());
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "unconfirmed@example.com", It.IsAny<Credential>()))
                    .CompletesWith(authUser);
                
                var controller = GetController<AuthenticationController>();
                
                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            Password = "thePassword",
                            EmailAddress = "unconfirmed@example.com",
                        }
                    }, "/theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();

                var expectedAddress = new MailAddress(authUser.User.UnconfirmedEmailAddress, authUser.User.Username);
                GetMock<IMessageService>()
                    .Verify(x => x.SendNewAccountEmail(
                        expectedAddress,
                        "https://nuget.local/account/confirm/theUsername/t0k3n"));
                ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
            }

            [Fact]
            public async Task WillNotSendConfirmationEmailWhenConfirmEmailAddressesIsOff()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    },
                    new Credential());
                var config = Get<ConfigurationService>();
                config.Current = new AppConfiguration()
                {
                    ConfirmEmailAddresses = false
                };
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "unconfirmed@example.com", It.IsAny<Credential>()))
                    .CompletesWith(authUser);

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            Password = "thePassword",
                            EmailAddress = "unconfirmed@example.com",
                        }
                    }, "/theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<IMessageService>()
                    .Verify(x => x.SendNewAccountEmail(
                        It.IsAny<MailAddress>(),
                        It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage()
            {
                // Arrange
                var authUser = new AuthenticatedUser(new User("theUsername"), new Credential());
                
                GetMock<AuthenticationService>(); // Force AuthenticationService to be mocked even though it's concrete
                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            EmailAddress = "theEmailAddress",
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                VerifyExternalLinkExpiredResult(controller, result);
                GetMock<AuthenticationService>()
                    .Verify(x => x.CreateSession(It.IsAny<IOwinContext>(), It.IsAny<User>()), Times.Never());
                GetMock<AuthenticationService>()
                    .Verify(x => x.Register("theUsername", "theEmailAddress", It.IsAny<Credential>()), Times.Never());
            }

            [Fact]
            public async Task GivenValidExternalAuth_ItCreatesAccountAndLinksCredential()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    }, 
                    new Credential());
                var externalCred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "theEmailAddress", externalCred))
                    .CompletesWith(authUser);

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSession(controller.OwinContext, authUser.User))
                    .Verifiable();
                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred
                    });

                // Simulate the model state error that will be added when doing an extenral account registration (since password is not present)
                controller.ModelState.AddModelError("Register.Password", "Password is required");

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            EmailAddress = "theEmailAddress",
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();

                var expectedAddress = new MailAddress(authUser.User.UnconfirmedEmailAddress, authUser.User.Username);
                GetMock<IMessageService>()
                    .Verify(x => x.SendNewAccountEmail(
                        expectedAddress,
                        "https://nuget.local/account/confirm/theUsername/t0k3n"));
                ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
            }
        }

        public class TheAuthenticateAction : TestContainer
        {
            [Fact]
            public void WillChallengeTheUserUsingTheGivenProviderAndReturnUrl()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();

                // Act
                var result = controller.Authenticate("/theReturnUrl", "MicrosoftAccount");

                // Assert
                ResultAssert.IsChallengeResult(result, "MicrosoftAccount", "/users/account/authenticate/return?ReturnUrl=%2FtheReturnUrl");
            }
        }

        public class TheLinkExternalAccountAction : TestContainer
        {
            [Fact]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage()
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                VerifyExternalLinkExpiredResult(controller, result);
            }

            [Fact]
            public async Task GivenAssociatedLocalUser_ItCreatesASessionAndSafeRedirectsToReturnUrl()
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var authUser = new AuthenticatedUser(
                    Fakes.CreateUser("test", cred),
                    cred);
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                GetMock<AuthenticationService>()
                    .Verify(x => x.CreateSession(controller.OwinContext, authUser.User));
            }

            [Fact]
            public async Task GivenNoLinkAndNoClaimData_ItDisplaysLogOnViewWithNoPrefilledData()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                
                GetMock<AuthenticationService>(); // Force a mock to be created
                
                var controller = GetController<AuthenticationController>();
                
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: "LogOn");
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.False(model.External.FoundExistingUser);
                Assert.Null(model.SignIn.UserNameOrEmail);
                Assert.Null(model.Register.EmailAddress);
            }

            [Fact]
            public async Task GivenNoLinkAndNameClaim_ItDisplaysLogOnViewWithExternalAccountName()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();

                GetMock<AuthenticationService>(); // Force a mock to be created

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(new[] { 
                            new Claim(ClaimTypes.Name, "Joe Bloggs")
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: "LogOn");
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Equal("Joe Bloggs", model.External.AccountName);
                Assert.False(model.External.FoundExistingUser);
                Assert.Null(model.SignIn.UserNameOrEmail);
                Assert.Null(model.Register.EmailAddress);
            }

            [Fact]
            public async Task GivenNoLinkAndEmailClaim_ItDisplaysLogOnViewWithEmailPrefilled()
            {
                // Arrange
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();

                GetMock<AuthenticationService>(); // Force a mock to be created

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(new[] { 
                            new Claim(ClaimTypes.Email, "blorg@example.com")
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: "LogOn");
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.False(model.External.FoundExistingUser);
                Assert.Equal("blorg@example.com", model.SignIn.UserNameOrEmail);
                Assert.Equal("blorg@example.com", model.Register.EmailAddress);
            }

            [Fact]
            public async Task GivenNoLinkButEmailMatchingLocalUser_ItDisplaysLogOnViewPresetForSignIn()
            {
                // Arrange
                var existingUser = new User("existingUser") { EmailAddress = "existing@example.com" };
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                var authUser = new AuthenticatedUser(
                    Fakes.CreateUser("test", cred),
                    cred);

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByEmailAddress(existingUser.EmailAddress))
                    .Returns(existingUser);

                var controller = GetController<AuthenticationController>();
                
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(new [] {
                            new Claim(ClaimTypes.Email, existingUser.EmailAddress)
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: "LogOn");
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.True(model.External.FoundExistingUser);
                Assert.Equal(existingUser.EmailAddress, model.SignIn.UserNameOrEmail);
                Assert.Equal(existingUser.EmailAddress, model.Register.EmailAddress);
            }
        }

        public static void VerifyExternalLinkExpiredResult(AuthenticationController controller, ActionResult result) {
            ResultAssert.IsRedirectToRoute(result, new { action = "LogOn" });
            Assert.Equal(Strings.ExternalAccountLinkExpired, controller.TempData["Message"]);
        }

        private static void EnableAllAuthenticators(AuthenticationService authService)
        {
            foreach (var auther in authService.Authenticators.Values)
            {
                auther.BaseConfig.Enabled = true;
            }
        }
    }
}

