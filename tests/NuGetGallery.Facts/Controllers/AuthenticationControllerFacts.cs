// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.AzureActiveDirectory;
using NuGetGallery.Authentication.Providers.AzureActiveDirectoryV2;
using NuGetGallery.Authentication.Providers.MicrosoftAccount;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class AuthenticationControllerFacts
    {
        private const string RegisterViewName = "Register";
        private const string SignInViewName = "SignIn";
        private const string SignInViewNuGetName = "SignInNuGetAccount";
        private const string LinkExternalViewName = "LinkExternal";

        public class TheLogOnAction : TestContainer
        {
            [Fact]
            public void GivenUserAlreadyAuthenticated_ItRedirectsToReturnUrl()
            {
                // Arrange
                var controller = GetController<AuthenticationController>();
                var fakes = Get<Fakes>();
                controller.SetCurrentUser(fakes.User);

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
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: SignInViewName);
                Assert.NotNull(model.SignIn);
                Assert.NotNull(model.Register);
                Assert.Equal(3, model.Providers.Count);

                var providerNames = model.Providers.Select(p => p.ProviderName);
                Assert.Contains("AzureActiveDirectoryV2", providerNames);
                Assert.Contains("AzureActiveDirectory", providerNames);
                Assert.Contains("MicrosoftAccount", providerNames);
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

            [Theory]
            [InlineData("account/profile")]
            [InlineData("Admin/SupportRequest")]
            public void WillNotRedirectToTheReturnUrlWhenReturnUrlContains(string returnUrl)
            {
                var controller = GetController<AuthenticationController>();

                var result = controller.LogOff(returnUrl);
                ResultAssert.IsSafeRedirectTo(result, null);
            }
        }

        public class TheSigninAssistanceAction : TestContainer
        {
            [Fact]
            public async Task NullUsernameReturnsFalse()
            {
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignInAssistance(username: null, providedEmailAddress: null);
                dynamic data = result.Data;
                Assert.False(data.success);
            }

            [Theory]
            [InlineData("random@address.com", "r**********m@address.com")]
            [InlineData("rm@address.com", "r**********m@address.com")]
            [InlineData("r@address.com", "r**********@address.com")]
            [InlineData("random.very.long.address@address.com", "r**********s@address.com")]
            public async Task NullProvidedEmailReturnsFormattedEmail(string email, string expectedEmail)
            {
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity: "John Doe <random@address.com>");
                var existingUser = new User("existingUser") { EmailAddress = email, Credentials = new[] { cred } };

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>(), false))
                    .Returns(existingUser);

                var controller = GetController<AuthenticationController>();

                var result = await controller.SignInAssistance(username: "existingUser", providedEmailAddress: null);
                dynamic data = result.Data;
                Assert.True(data.success);
                Assert.Equal(expectedEmail, data.EmailAddress);
            }

            [Theory]
            [InlineData("random@address.com", "r**********m@address.com")]
            [InlineData("rm@address.com", "r**********m@address.com")]
            [InlineData("r@address.com", "r**********@address.com")]
            [InlineData("random.very.long.address@address.com", "r**********s@address.com")]
            public async Task NullProvidedEmailReturnsFormattedEmailForUnconfirmedAccount(string email, string expectedEmail)
            {
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity: "John Doe <random@address.com>");
                var existingUser = new User("existingUser") { UnconfirmedEmailAddress = email, Credentials = new[] { cred } };

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>(), false))
                    .Returns(existingUser);

                var controller = GetController<AuthenticationController>();

                var result = await controller.SignInAssistance(username: "existingUser", providedEmailAddress: null);
                dynamic data = result.Data;
                Assert.True(data.success);
                Assert.Equal(expectedEmail, data.EmailAddress);
            }

            [Theory]
            [InlineData("blarg")]
            [InlineData("wrong@email")]
            [InlineData("nonmatching@emailaddress.com")]
            public async Task InvalidProvidedEmailReturnsFalse(string providedEmail)
            {
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity: "existing@example.com");
                var existingUser = new User("existingUser") { EmailAddress = "existing@example.com", Credentials = new[] { cred } };

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>(), false))
                    .Returns(existingUser);

                var controller = GetController<AuthenticationController>();

                var result = await controller.SignInAssistance(username: "existingUser", providedEmailAddress: providedEmail);
                dynamic data = result.Data;
                Assert.False(data.success);
            }

            [Fact]
            public async Task SendsNotificationForAssistance()
            {
                var email = "existing@example.com";
                var fakes = Get<Fakes>();
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity: "existing@example.com");
                var existingUser = new User("existingUser") { EmailAddress = email, Credentials = new[] { cred } };

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(It.IsAny<string>(), false))
                    .Returns(existingUser);
                var messageServiceMock = GetMock<IMessageService>();
                messageServiceMock
                .Setup(m => m.SendMessageAsync(It.IsAny<SigninAssistanceMessage>(), false, false))
                .Returns(Task.CompletedTask)
                .Verifiable();

                var controller = GetController<AuthenticationController>();

                var result = await controller.SignInAssistance(username: "existingUser", providedEmailAddress: email);
                dynamic data = result.Data;
                Assert.True(data.success);
                messageServiceMock.Verify();
            }
        }

        public class TheSignInAction : TestContainer
        {
            [Fact]
            public async Task GivenUserAlreadyAuthenticated_ItRedirectsToReturnUrl()
            {
                // Arrange
                var controller = GetController<AuthenticationController>();
                var fakes = Get<Fakes>();
                controller.SetCurrentUser(fakes.User);

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

                ResultAssert.IsView(result, viewName: SignInViewNuGetName, viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public async Task WillInvalidateModelStateAndShowTheViewWithErrorsWhenTheUsernameAndPasswordAreNotValid()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWith(new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.BadCredentials));
                var controller = GetController<AuthenticationController>();

                var result = await controller.SignIn(
                    new LogOnViewModel() { SignIn = new SignInViewModel() },
                    "theReturnUrl", linkingAccount: false);

                ResultAssert.IsView(result, viewName: SignInViewNuGetName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UsernameAndPasswordNotFound, controller.ModelState[SignInViewName].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task CanLogTheUserOnWithUserName()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential { Type = "Foo" });
                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authResult);

                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
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
                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .CompletesWith(authResult);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
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
                    new Credential { Type = "Foo" });
                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);
                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .CompletesWith(authResult);
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(a => a.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
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

            public async Task WhenAttemptingToLinkExternalToExistingAccountWithNoExternalAccounts_AllowsLinkingAndRemovesPassword()
            {
                // Arrange
                var passwordCredential = new Credential(CredentialTypes.Password.Prefix, "thePassword");
                var user = new User("theUsername") { EmailAddress = "confirmed@example.com", Credentials = new[] { passwordCredential } };

                var authUser = new AuthenticatedUser(
                    user,
                    new Credential() { Type = "foo" });

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .CompletesWith(authResult);

                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(It.IsAny<OwinContext>()))
                    .Returns(Task.FromResult(new AuthenticateExternalLoginResult { ExternalIdentity = new ClaimsIdentity() }));

                var controller = GetController<AuthenticationController>();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            "confirmed@example.com",
                            "thePassword")),
                    "theReturnUrl", linkingAccount: true);

                // Assert
                GetMock<AuthenticationService>()
                    .Verify(x => x.AddCredential(It.IsAny<User>(), It.IsAny<Credential>()));

                GetMock<AuthenticationService>()
                    .Verify(x => x.CreateSessionAsync(controller.OwinContext, authUser, false));

                GetMock<AuthenticationService>()
                    .Verify(x => x.RemoveCredential(user, passwordCredential, true));

                GetMock<IMessageService>()
                    .Verify(x => x.SendMessageAsync(It.IsAny<CredentialAddedMessage>(), false, false));
            }

            public async Task WhenAttemptingToLinkExternalToAccountWithExistingExternals_RejectsLinking()
            {
                // Arrange
                var user = new User("theUsername")
                {
                    EmailAddress = "confirmed@example.com",
                    Credentials = new[] { new Credential { Type = CredentialTypes.External.Prefix + "Foo" } }
                };

                var authUser = new AuthenticatedUser(
                    user,
                    new Credential() { Type = "Foo" });

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .CompletesWith(authResult);
                var controller = GetController<AuthenticationController>();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            "confirmed@example.com",
                            "thePassword")),
                    "theReturnUrl", linkingAccount: true);

                // Assert
                GetMock<AuthenticationService>().Verify(a => a.CreateSessionAsync(controller.OwinContext, authUser, false), Times.Never());
                ResultAssert.IsView(result, viewName: SignInViewNuGetName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.AccountIsLinkedToAnotherExternalAccount, controller.ModelState[SignInViewName].Errors[0].ErrorMessage);
            }

            public async Task WhenAttemptingToLinkExternalToAdminUserWithExistingExternals_AllowsLinking()
            {
                // Arrange
                var user = new User("theUsername")
                {
                    EmailAddress = "confirmed@example.com",
                    Credentials = new[]
                    {
                        new Credential { Type = CredentialTypes.External.Prefix + "Foo" }
                    },
                    Roles = new[]
                    {
                        new Role { Name = CoreConstants.AdminRoleName }
                    }
                };

                var authUser = new AuthenticatedUser(
                    user,
                    new Credential() { Type = CredentialTypes.External.Prefix + "foo" });

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate("confirmed@example.com", "thePassword"))
                    .CompletesWith(authResult);

                var externalCredential = new Credential { Type = "externalcred" };

                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(It.IsAny<OwinContext>()))
                    .Returns(Task.FromResult(new AuthenticateExternalLoginResult { Credential = externalCredential, ExternalIdentity = new ClaimsIdentity() }));

                var credentialViewModel = new CredentialViewModel();
                GetMock<AuthenticationService>()
                    .Setup(x => x.DescribeCredential(externalCredential))
                    .Returns(credentialViewModel);

                var controller = GetController<AuthenticationController>();

                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<CredentialAddedMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.CredentialType == credentialViewModel.GetCredentialTypeInfo()),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await controller.SignIn(
                    new LogOnViewModel(
                        new SignInViewModel(
                            "confirmed@example.com",
                            "thePassword")),
                    "theReturnUrl", linkingAccount: true);

                // Assert
                GetMock<AuthenticationService>()
                    .Verify(x => x.AddCredential(authUser.User, externalCredential));

                GetMock<AuthenticationService>()
                    .Verify(x => x.CreateSessionAsync(controller.OwinContext, authUser, false));

                messageService
                    .Verify(svc => svc.SendMessageAsync(
                        It.IsAny<CredentialAddedMessage>(),
                        false,
                        false));
            }

            [Fact]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential { Type = "Foo" });

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authResult);

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
                    .Verify(x => x.CreateSessionAsync(It.IsAny<IOwinContext>(), It.IsAny<AuthenticatedUser>(), false), Times.Never());
            }

            [Fact]
            public async Task GivenValidExternalAuth_ItLinksCredentialSendsEmailAndLogsIn()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername") { EmailAddress = "confirmed@example.com" },
                    new Credential { Type = "Foo" });
                var externalCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authResult);
                GetMock<AuthenticationService>()
                    .Setup(x => x.AddCredential(authUser.User, externalCred))
                    .Completes()
                    .Verifiable();

                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<CredentialAddedMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.CredentialType.Type == CredentialTypes.External.MicrosoftAccount),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, It.IsAny<AuthenticatedUser>(), false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();
                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult
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

            [Theory]
            [InlineData("MicrosoftAccount", true)]
            [InlineData("AzureActiveDirectory", false)]
            public async Task GivenAdminLogsInWithValidExternalAuth_ItChallengesWhenNotUsingRequiredExternalProvider(string providerUsedForLogin, bool shouldChallenge)
            {
                var enforcedProvider = "AzureActiveDirectory";

                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;
                configurationService.Current.EnforcedAuthProviderForAdmin = enforcedProvider;

                var externalCred = new CredentialBuilder().CreateExternalCredential(providerUsedForLogin, "blorg", "Bloog");

                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "confirmed@example.com",
                        Roles =
                        {
                            new Role { Name = CoreConstants.AdminRoleName }
                        }
                    },
                    externalCred);

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, authUser);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Authenticate(authUser.User.Username, "thePassword"))
                    .CompletesWith(authResult);

                GetMock<AuthenticationService>()
                    .Setup(x => x.AddCredential(authUser.User, externalCred))
                    .Completes()
                    .Verifiable();

                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<CredentialAddedMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.CredentialType.Type == CredentialTypes.External.Prefix + providerUsedForLogin),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();

                if (shouldChallenge)
                {
                    GetMock<AuthenticationService>()
                        .Setup(x => x.Challenge(enforcedProvider, It.IsAny<string>(), null))
                        .Returns(new ChallengeResult(enforcedProvider, null, null))
                        .Verifiable();
                }
                else
                {
                    GetMock<AuthenticationService>()
                       .Setup(x => x.CreateSessionAsync(controller.OwinContext, It.IsAny<AuthenticatedUser>(), false))
                       .Returns(Task.FromResult(0))
                       .Verifiable();
                }

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
                if (shouldChallenge)
                {
                    ResultAssert.IsChallengeResult(result, enforcedProvider);
                }
                else
                {
                    ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                }
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

                ResultAssert.IsView(result, viewName: RegisterViewName, viewData: new
                {
                    ReturnUrl = (string)null
                });
            }

            [Fact]
            public async Task WillInvalidateModelStateAndShowTheViewWhenAnEntityExceptionIsThrow()
            {
                GetMock<AuthenticationService>()
                    .Setup(x => x.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Credential>(), It.IsAny<bool>()))
                    .Throws(new EntityException("aMessage"));

                var controller = GetController<AuthenticationController>();

                var request = new LogOnViewModel()
                {
                    Register = new RegisterViewModel
                    {
                        Username = "theUsername",
                        Password = "thePassword",
                        EmailAddress = "someone@example.com",
                    }
                };
                var result = await controller.Register(request, null, linkingAccount: false);

                ResultAssert.IsView(result, viewName: RegisterViewName);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("aMessage", controller.ModelState["Register"].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillCreateAndLogInTheUserWhenNotLinking()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    },
                    new Credential());

                var authenticationService = GetMock<AuthenticationService>();
                var controller = GetController<AuthenticationController>();

                authenticationService.Setup(x => x.Register(authUser.User.Username, authUser.User.UnconfirmedEmailAddress, It.IsAny<Credential>(), It.IsAny<bool>()))
                    .CompletesWith(authUser);

                authenticationService
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = authUser.User.Username,
                            Password = "thePassword",
                            EmailAddress = authUser.User.UnconfirmedEmailAddress,
                        }
                    }, "/theReturnUrl", linkingAccount: false);

                // Assert
                GetMock<AuthenticationService>().VerifyAll();

                GetMock<IMessageService>()
                    .Verify(x => x.SendMessageAsync(
                        It.Is<NewAccountMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.ConfirmationUrl == TestUtility.GallerySiteRootHttps + "account/confirm/" + authUser.User.Username + "/" + authUser.User.EmailConfirmationToken),
                        false,
                        false));

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

                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;

                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "unconfirmed@example.com", It.IsAny<Credential>(), It.IsAny<bool>()))
                    .CompletesWith(authUser);

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
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
                GetMock<AuthenticationService>()
                    .Verify(x => x.Register("theUsername", "unconfirmed@example.com", It.IsAny<Credential>(), false));

                GetMock<IMessageService>()
                    .Verify(
                    x => x.SendMessageAsync(
                        It.IsAny<NewAccountMessage>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Never());
            }

            [Fact]
            public async Task WillNotAutoConfirmAndWillSendConfirmationEmailWhenNotExternalCredential()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    },
                    new Credential());

                var authenticationServiceMock = GetMock<AuthenticationService>();
                var controller = GetController<AuthenticationController>();
                authenticationServiceMock
                    .Setup(x => x.Register(authUser.User.Username, authUser.User.UnconfirmedEmailAddress, It.IsAny<Credential>(), It.IsAny<bool>()))
                    .CompletesWith(authUser);
                authenticationServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();
                authenticationServiceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = new Credential(),
                        UserInfo = new IdentityInformation("", "", authUser.User.UnconfirmedEmailAddress, "")
                    });

                var confirmationUrl = TestUtility.GallerySiteRootHttps + "account/confirm/" + authUser.User.Username + "/" + authUser.User.EmailConfirmationToken;
                var configurationService = GetConfigurationService();
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<NewAccountMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.ConfirmationUrl == confirmationUrl),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            EmailAddress = authUser.User.UnconfirmedEmailAddress,
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                authenticationServiceMock.VerifyAll();

                GetMock<AuthenticationService>()
                    .Verify(x => x.Register(authUser.User.Username, authUser.User.UnconfirmedEmailAddress, It.IsAny<Credential>(), false));

                messageService.Verify();

                ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
            }

            [Fact]
            public async Task WillNotAutoConfirmAndWillSendConfirmationEmailWhenModelRegisterEmailAndExternalCredentialEmailNotMatch()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    },
                    new Credential());

                var externalCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");

                var authenticationServiceMock = GetMock<AuthenticationService>();
                var controller = GetController<AuthenticationController>();
                authenticationServiceMock
                    .Setup(x => x.Register(authUser.User.Username, "anotherunconfirmed@example.com", externalCred, It.IsAny<bool>()))
                    .CompletesWith(authUser);
                authenticationServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();
                authenticationServiceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred,
                        UserInfo = new IdentityInformation("", "", "unconfirmed@example.com", "")
                    });

                var confirmationUrl = TestUtility.GallerySiteRootHttps + "account/confirm/" + authUser.User.Username + "/" + authUser.User.EmailConfirmationToken;
                var configurationService = GetConfigurationService();
                var messageService = GetMock<IMessageService>();
                messageService
                    .Setup(svc => svc.SendMessageAsync(
                        It.Is<NewAccountMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.ConfirmationUrl == confirmationUrl),
                        false,
                        false))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            EmailAddress = "anotherunconfirmed@example.com",
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                authenticationServiceMock.VerifyAll();

                GetMock<AuthenticationService>()
                    .Verify(x => x.Register(authUser.User.Username, "anotherunconfirmed@example.com", externalCred, false));

                messageService.Verify();

                ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
            }

            [Fact]
            public async Task WillAutoConfirmAndWillNotSendConfirmationEmailWhenIsExternalCredentialAndModelRegisterEmailAndExternalCredentialEmailMatch()
            {
                // Arrange
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n"
                    },
                    new Credential());

                var externalCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");

                var authenticationServiceMock = GetMock<AuthenticationService>();
                var controller = GetController<AuthenticationController>();
                authenticationServiceMock
                    .Setup(x => x.Register(authUser.User.Username, authUser.User.UnconfirmedEmailAddress, externalCred, It.IsAny<bool>()))
                    .CompletesWith(authUser)
                    .Callback(() =>
                     {
                         authUser.User.EmailAddress = authUser.User.UnconfirmedEmailAddress;
                         authUser.User.UnconfirmedEmailAddress = null;
                     });

                authenticationServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();
                authenticationServiceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred,
                        UserInfo = new IdentityInformation("", "", authUser.User.UnconfirmedEmailAddress, "")
                    });

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = "theUsername",
                            EmailAddress = authUser.User.UnconfirmedEmailAddress,
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                authenticationServiceMock.VerifyAll();

                GetMock<AuthenticationService>()
                    .Verify(x => x.Register(authUser.User.Username, authUser.User.EmailAddress, externalCred, true));

                GetMock<IMessageService>()
                    .Verify(x => x.SendMessageAsync(
                        It.IsAny<NewAccountMessage>(), It.IsAny<bool>(), It.IsAny<bool>()),
                        Times.Never());
            }

            [Fact]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage()
            {
                // Arrange
                var authUser = new AuthenticatedUser(new User("theUsername"), new Credential());

                GetMock<AuthenticationService>(); // Force AuthenticationService to be mocked even though it's concrete
                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
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
                    .Verify(x => x.CreateSessionAsync(It.IsAny<IOwinContext>(), It.IsAny<AuthenticatedUser>(), false), Times.Never());
                GetMock<AuthenticationService>()
                    .Verify(x => x.Register("theUsername", "theEmailAddress", It.IsAny<Credential>(), It.IsAny<bool>()), Times.Never());
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

                var externalCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");

                var authenticationServiceMock = GetMock<AuthenticationService>();
                var controller = GetController<AuthenticationController>();
                authenticationServiceMock
                    .Setup(x => x.Register(authUser.User.Username, authUser.User.UnconfirmedEmailAddress, externalCred, It.IsAny<bool>()))
                    .CompletesWith(authUser);
                authenticationServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();

                authenticationServiceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred,
                        UserInfo = new IdentityInformation("", "", "", "")
                    });

                // Simulate the model state error that will be added when doing an external account registration (since password is not present)
                controller.ModelState.AddModelError("Register.Password", "Password is required");

                // Act
                var result = await controller.Register(
                    new LogOnViewModel()
                    {
                        Register = new RegisterViewModel
                        {
                            Username = authUser.User.Username,
                            EmailAddress = authUser.User.UnconfirmedEmailAddress,
                        }
                    }, "/theReturnUrl", linkingAccount: true);

                // Assert
                authenticationServiceMock.VerifyAll();

                GetMock<IMessageService>()
                    .Verify(x => x.SendMessageAsync(
                        It.Is<NewAccountMessage>(
                            msg =>
                            msg.User == authUser.User
                            && msg.ConfirmationUrl == TestUtility.GallerySiteRootHttps + "account/confirm/" + authUser.User.Username + "/" + authUser.User.EmailConfirmationToken),
                        false,
                        false));

                ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
            }

            [Theory]
            [InlineData("MicrosoftAccount", true)]
            [InlineData("AzureActiveDirectory", false)]
            public async Task GivenAdminLogsInWithExternalIdentity_ItChallengesWhenNotUsingRequiredExternalProvider(string providerUsedForLogin, bool shouldChallenge)
            {
                // Arrange
                var enforcedProvider = "AzureActiveDirectory";

                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;
                configurationService.Current.EnforcedAuthProviderForAdmin = enforcedProvider;

                var externalCred = new CredentialBuilder().CreateExternalCredential(providerUsedForLogin, "blorg", "Bloog");

                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        UnconfirmedEmailAddress = "unconfirmed@example.com",
                        EmailConfirmationToken = "t0k3n",
                        Roles =
                        {
                            new Role { Name = CoreConstants.AdminRoleName }
                        }
                    },
                    externalCred);

                GetMock<AuthenticationService>()
                    .Setup(x => x.Register("theUsername", "theEmailAddress", externalCred, It.IsAny<bool>()))
                    .CompletesWith(authUser);

                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();

                if (shouldChallenge)
                {
                    GetMock<AuthenticationService>()
                        .Setup(x => x.Challenge(enforcedProvider, It.IsAny<string>(), null))
                        .Returns(new ChallengeResult(enforcedProvider, null, null))
                        .Verifiable();
                }
                else
                {
                    GetMock<AuthenticationService>()
                       .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                       .Returns(Task.FromResult(0))
                       .Verifiable();
                }

                GetMock<AuthenticationService>()
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = externalCred,
                        UserInfo = new IdentityInformation("", "", "theEmailAddress", "")
                    });

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
                if (shouldChallenge)
                {
                    ResultAssert.IsChallengeResult(result, enforcedProvider);
                }
                else
                {
                    ResultAssert.IsSafeRedirectTo(result, "/theReturnUrl");
                }
                GetMock<AuthenticationService>().VerifyAll();
            }
        }

        public class TheAuthenticateAction : TestContainer
        {
            [Fact]
            public void WillChallengeTheUserUsingTheGivenProviderAndReturnUrl()
            {
                // Arrange
                const string returnUrl = "/theReturnUrl";
                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();

                // Act
                var result = controller.AuthenticateAndLinkExternal(returnUrl, "MicrosoftAccount");

                // Assert
                ResultAssert.IsChallengeResult(result, "MicrosoftAccount", "/users/account/authenticate/return?ReturnUrl=" + HttpUtility.UrlEncode(returnUrl));
            }
        }

        public class TheLinkOrChangeExternalCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenExpiredExternalAuth_ItSafeRedirectsToReturnUrlWithExternalAuthExpiredMessage()
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var serviceMock = GetMock<AuthenticationService>();
                serviceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.LinkOrChangeExternalCredential("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, expectedUrl: "theReturnUrl");
                Assert.Equal(Strings.ExternalAccountLinkExpired, controller.TempData["ErrorMessage"]);
            }

            [Fact]
            public async Task GivenExistingCredential_ItSafeRedirectsToReturnUrlWithErrorMessage()
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var identity = "Bloog <bloog@blorg.com>";
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity);
                var serviceMock = GetMock<AuthenticationService>();
                serviceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = null,
                        Credential = cred
                    });

                serviceMock
                    .Setup(x => x.TryReplaceCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .CompletesWith(false);

                // Act
                var result = await controller.LinkOrChangeExternalCredential("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                
                var errorMessage = controller.TempData["RawErrorMessage"];
                var expectedMessage = string.Format(
                    Strings.ChangeCredential_Failed,
                    identity.Replace("<", "&lt;").Replace(">", "&gt;"),
                    UriExtensions.GetExternalUrlAnchorTag("FAQs page", GalleryConstants.FAQLinks.MSALinkedToAnotherAccount));

                Assert.NotNull(errorMessage);
                Assert.Equal(expectedMessage, errorMessage);
            }

            [Fact]
            public async Task GivenNewCredential_ItSuccessfullyReplacesExternalCredentialsAndRemovesPasswordCredential()
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var identity = "Bloog";
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", identity);
                var passwordCred = new Credential("password.v3", "bloopbloop");
                var email = "bloog@blorg.com";
                var externalAuthenticator = GetMock<Authenticator>();
                externalAuthenticator
                    .Setup(x => x.GetIdentityInformation(It.IsAny<ClaimsIdentity>()))
                    .Returns(new IdentityInformation("", "", email, ""));
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", cred, passwordCred);
                user.EmailAddress = email;
                controller.SetCurrentUser(user);
                var authUser = new AuthenticatedUser(
                    user, cred);
                var serviceMock = GetMock<AuthenticationService>();
                serviceMock
                    .Setup(x => x.ReadExternalLoginCredential(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = null,
                        Authenticator = externalAuthenticator.Object,
                        Credential = cred
                    })
                    .Verifiable();

                serviceMock
                    .Setup(x => x.TryReplaceCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .CompletesWith(true)
                    .Verifiable();

                serviceMock
                    .Setup(x => x.Authenticate(It.IsAny<Credential>()))
                    .CompletesWith(authUser)
                    .Verifiable();

                serviceMock
                    .Setup(x => x.CreateSessionAsync(It.IsAny<IOwinContext>(), authUser, false))
                    .Completes()
                    .Verifiable();

                serviceMock
                    .Setup(x => x.RemoveCredential(user, passwordCred, true))
                    .Completes()
                    .Verifiable();

                // Act
                var result = await controller.LinkOrChangeExternalCredential("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                Assert.Equal(string.Format(Strings.ChangeCredential_Success, email), controller.TempData["Message"]);
                serviceMock.VerifyAll();
            }
        }

        public class TheAuthenticateExternalAction : TestContainer
        {

            [Fact]
            public void ForMissingExternalProvider_ErrorIsReturned()
            {
                var controller = GetController<AuthenticationController>();

                // Act
                var result = controller.AuthenticateExternal("theReturnUrl");

                // Assert
                ResultAssert.IsRedirectTo(result, "theReturnUrl");
                Assert.Equal(Strings.ChangeCredential_ProviderNotFound, controller.TempData["Message"]);
            }

            [Fact]
            public void ForAADLinkedAccount_ErrorIsReturnedDueToOrgPolicy()
            {
                var fakes = Get<Fakes>();
                var aadCred = new CredentialBuilder().CreateExternalCredential("AzureActiveDirectory", "blorg", "bloog", "TEST_TENANT");
                var passwordCred = new Credential("password.v3", "random");
                var msftCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "bloom", "filter");
                var user = fakes.CreateUser("test", aadCred, passwordCred, msftCred);
                var org = fakes.Organization;
                RequireOrganizationTenantPolicy
                    .Create("TEST_TENANT")
                    .Policies
                    .ToList()
                    .ForEach(policy => org.SecurityPolicies.Add(policy));
                user.Organizations.Add(new Membership() { Organization = org });

                var controller = GetController<AuthenticationController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.AuthenticateExternal("theReturnUrl");

                // Assert
                ResultAssert.IsRedirectTo(result, "theReturnUrl");
                Assert.NotNull(controller.TempData["WarningMessage"]);
            }

            [Fact]
            public void ForNonAADLinkedAccount_WithOrgPolicyCompletesSuccessfully()
            {
                var fakes = Get<Fakes>();
                EnableAllAuthenticators(Get<AuthenticationService>());
                var passwordCred = new Credential("password.v3", "random");
                var msftCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "bloom", "filter");
                var user = fakes.CreateUser("test", passwordCred, msftCred);
                var org = fakes.Organization;
                RequireOrganizationTenantPolicy
                    .Create("TEST_TENANT")
                    .Policies
                    .ToList()
                    .ForEach(policy => org.SecurityPolicies.Add(policy));
                user.Organizations.Add(new Membership() { Organization = org });

                var controller = GetController<AuthenticationController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.AuthenticateExternal("theReturnUrl");

                // Assert
                Assert.Null(controller.TempData["WarningMessage"]);
                ResultAssert.IsChallengeResult(result, "AzureActiveDirectoryV2", controller.Url.LinkOrChangeExternalCredential("theReturnUrl"));
            }

            [Theory]
            [InlineData("MicrosoftAccount")]
            [InlineData("AzureActiveDirectory")]
            public void WillCallChallengeAuthenticationForAADv2ProviderForUserWithNoAADCredential(string credType)
            {
                // Arrange
                const string returnUrl = "/theReturnUrl";
                EnableAllAuthenticators(Get<AuthenticationService>());

                var fakes = Get<Fakes>();
                var passwordCred = new Credential("password.v3", "random");
                var msftCred = new CredentialBuilder().CreateExternalCredential(credType, "bloom", "filter");
                var user = fakes.CreateUser("test", passwordCred, msftCred);
                var controller = GetController<AuthenticationController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.AuthenticateExternal(returnUrl);

                // Assert
                ResultAssert.IsChallengeResult(result, "AzureActiveDirectoryV2", controller.Url.LinkOrChangeExternalCredential(returnUrl));
            }

            [Fact]
            public void WillCallChallengeAuthenticationForAADv2Provider()
            {
                // Arrange
                const string returnUrl = "/theReturnUrl";
                EnableAllAuthenticators(Get<AuthenticationService>());
                var controller = GetController<AuthenticationController>();

                // Act
                var result = controller.AuthenticateExternal(returnUrl);

                // Assert
                ResultAssert.IsChallengeResult(result, "AzureActiveDirectoryV2", controller.Url.LinkOrChangeExternalCredential(returnUrl));
            }
        }

        public class TheLinkExternalAccountAction : TestContainer
        {
            [Theory]
            [InlineData("access_denied")]
            [InlineData("consent_required")]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithExternalAuthExpiredMessage(string error)
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl", error);

                // Assert
                VerifyExternalLinkExpiredResult(controller, result);
            }

            [Theory]
            [InlineData("server_error", "The server encountered an unexpected error.")]
            [InlineData("temporarily_unavailable", "The server is temporarily too busy to handle the request.")]
            [InlineData("invalid_resource", "The target resource is invalid because either it does not exist, Azure AD cannot find it, or it is not correctly configured.")]
            [InlineData("invalid_request", "Protocol error, such as a missing, required parameter.")]
            public async Task GivenExpiredExternalAuth_ItRedirectsBackToLogOnWithPassedErrorMessage(string error, string errorDescription)
            {
                // Arrange
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult());

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl", error, errorDescription);

                // Assert
                VerifyExternalLinkExpiredResult(controller, result, errorDescription);
            }

            [Fact]
            public async Task GivenAssociatedLocalUser_ItCreatesASessionAndSafeRedirectsToReturnUrl()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
                    cred);

                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser,
                        Credential = cred
                    });

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                GetMock<AuthenticationService>()
                    .VerifyAll();
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task GivenDiscontinuedLoginSetting_ItRemovesPasswordLoginAsAppropriate(bool discontinuedLogin)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var builder = new CredentialBuilder();
                var cred = builder.CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var passwordCred = builder.CreatePasswordCredential("scret_password");
                var user = fakes.CreateUser("test", cred, passwordCred);
                var authUser = new AuthenticatedUser(user, cred);

                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                controller.NuGetContext.Config.Current.DeprecateNuGetPasswordLogins = true;

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser,
                        Credential = cred
                    });

                GetMock<AuthenticationService>()
                    .Setup(x => x.RemoveCredential(user, passwordCred, true))
                    .Completes()
                    .Verifiable();

                var passwordConfigMock = new Mock<ILoginDiscontinuationConfiguration>();
                passwordConfigMock
                    .Setup(x => x.IsPasswordLoginDiscontinuedForAll())
                    .Returns(discontinuedLogin);

                GetMock<IContentObjectService>()
                    .Setup(x => x.LoginDiscontinuationConfiguration)
                    .Returns(passwordConfigMock.Object);

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                GetMock<AuthenticationService>()
                    .Verify(x => x.RemoveCredential(user, passwordCred, true), discontinuedLogin ? Times.Once() : Times.Never());
            }

            [Fact]
            public async Task ShouldChallengeForEnforcedMultiFactorAuthentication()
            {
                // Arrange
                var enforcedProvider = "AzureActiveDirectoryV2";
                var email = "test@email.com";
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var user = Get<Fakes>().CreateUser("test", cred);
                user.EnableMultiFactorAuthentication = true;
                var authServiceMock = GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var authUser = new AuthenticatedUser(
                    user,
                    cred);

                authServiceMock
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser,
                        Authenticator = new AzureActiveDirectoryV2Authenticator(),
                        LoginDetails = new ExternalLoginSessionDetails(email, usedMultiFactorAuthentication: false)
                    });

                var returnUrl = "theReturnUrl";
                authServiceMock
                    .Setup(x => x.Challenge(enforcedProvider, It.IsAny<string>(), It.Is<AuthenticationPolicy>((policy) => policy.EnforceMultiFactorAuthentication == true && policy.Email == email)))
                    .Verifiable();

                // Act
                var result = await controller.LinkExternalAccount(returnUrl);

                // Assert
                authServiceMock
                    .VerifyAll();
            }

            [Theory]
            [InlineData("MicrosoftAccount", true)]
            [InlineData("AzureActiveDirectory", false)]
            public async Task ShouldUpdateMultiFactorSettingForExternalAccounts(string credType, bool showMessage)
            {
                // Arrange
                var email = "test@email.com";
                var cred = new CredentialBuilder().CreateExternalCredential(credType, "blorg", "Bloog");
                var user = Get<Fakes>().CreateUser("test", cred);
                user.EnableMultiFactorAuthentication = false;
                var authServiceMock = GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var userServiceMock = GetMock<IUserService>();
                var authUser = new AuthenticatedUser(user, cred);

                authServiceMock
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = cred,
                        Authentication = authUser,
                        Authenticator = new AzureActiveDirectoryV2Authenticator(),
                        LoginDetails = new ExternalLoginSessionDetails(email, usedMultiFactorAuthentication: true)
                    });

                authServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, It.IsAny<bool>()))
                    .Returns(Task.CompletedTask);

                userServiceMock
                    .Setup(x => x.ChangeMultiFactorAuthentication(authUser.User, true, null))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var returnUrl = "theReturnUrl";

                // Act
                var result = await controller.LinkExternalAccount(returnUrl);

                // Assert
                authServiceMock.VerifyAll();
                userServiceMock.Verify(x => x.ChangeMultiFactorAuthentication(authUser.User, true, It.IsAny<string>()), Times.Once());
                if (showMessage)
                {
                    Assert.Equal(Strings.MultiFactorAuth_LoginUpdate, controller.TempData["Message"]);
                }

                ResultAssert.IsSafeRedirectTo(result, returnUrl);
            }

            [Theory]
            [InlineData("MicrosoftAccount", true)]
            [InlineData("AzureActiveDirectory", false)]
            public async Task ShouldAskToEnableMultiFactorSettingForMicrosoftAccounts(string credType, bool shouldAskToEnable2FA)
            {
                // Arrange
                var email = "test@email.com";
                var cred = new CredentialBuilder().CreateExternalCredential(credType, "blorg", "Bloog");
                var user = Get<Fakes>().CreateUser("test", cred);
                user.EnableMultiFactorAuthentication = false;
                var authServiceMock = GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var userServiceMock = GetMock<IUserService>();
                var authUser = new AuthenticatedUser(user, cred);

                authServiceMock
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Credential = cred,
                        Authentication = authUser,
                        Authenticator = new AzureActiveDirectoryV2Authenticator(),
                        LoginDetails = new ExternalLoginSessionDetails(email, usedMultiFactorAuthentication: false)
                    });

                authServiceMock
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, It.IsAny<bool>()))
                    .Returns(Task.CompletedTask);

                userServiceMock
                    .Setup(x => x.ChangeMultiFactorAuthentication(authUser.User, true, null))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var returnUrl = "theReturnUrl";

                // Act
                var result = await controller.LinkExternalAccount(returnUrl);

                // Assert
                authServiceMock.VerifyAll();
                userServiceMock.Verify(x => x.ChangeMultiFactorAuthentication(authUser.User, true, It.IsAny<string>()), Times.Never());
                if (shouldAskToEnable2FA)
                {
                    Assert.Equal(true, controller.TempData[GalleryConstants.AskUserToEnable2FA]);
                }

                ResultAssert.IsSafeRedirectTo(result, returnUrl);
            }

            [Theory]
            [InlineData("AzureActiveDirectory", false)]
            [InlineData("AzureActiveDirectory", true)]
            public async Task GivenAssociatedLocalAdminUser_ItVerifiesTheEnforcedTenantId(string providerUsedForLogin, bool shouldError)
            {
                // Arrange
                var enforcedTenantId = "Some-Tenant-Id";

                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;
                configurationService.Current.EnforcedTenantIdForAdmin = enforcedTenantId;

                var fakes = Get<Fakes>();
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var cred = new CredentialBuilder().CreateExternalCredential(providerUsedForLogin, "blorg", "Bloog", tenantId: shouldError ? "non-enforced-tenant-id" : enforcedTenantId);
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
                    cred);

                authUser.User.Roles.Add(new Role { Name = CoreConstants.AdminRoleName });

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser,
                        Credential = cred
                    });

                GetMock<AuthenticationService>()
                    .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                    .Returns(Task.FromResult(0))
                    .Verifiable();

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                if (shouldError)
                {
                    var expectedMessage = string.Format(Strings.SiteAdminNotLoggedInWithRequiredTenant, enforcedTenantId);
                    VerifyExternalLinkExpiredResult(controller, result, expectedMessage);
                } else
                {
                    ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                    GetMock<AuthenticationService>().VerifyAll();
                }
            }

            [Theory]
            [InlineData("MicrosoftAccount", true)]
            [InlineData("AzureActiveDirectory", false)]
            public async Task GivenAssociatedLocalAdminUser_ItChallengesWhenNotUsingRequiredExternalProvider(string providerUsedForLogin, bool shouldChallenge)
            {
                // Arrange
                var enforcedProvider = "AzureActiveDirectory";

                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;
                configurationService.Current.EnforcedAuthProviderForAdmin = enforcedProvider;

                var fakes = Get<Fakes>();
                GetMock<AuthenticationService>(); // Force a mock to be created
                var controller = GetController<AuthenticationController>();
                var cred = new CredentialBuilder().CreateExternalCredential(providerUsedForLogin, "blorg", "Bloog");
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
                    cred);

                authUser.User.Roles.Add(new Role { Name = CoreConstants.AdminRoleName });

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(),
                        Authentication = authUser,
                        Credential = cred
                    });

                if (shouldChallenge)
                {
                    GetMock<AuthenticationService>()
                        .Setup(x => x.Challenge(enforcedProvider, It.IsAny<string>(), null))
                        .Returns(new ChallengeResult(enforcedProvider, null, null))
                        .Verifiable();
                }
                else
                {
                    GetMock<AuthenticationService>()
                       .Setup(x => x.CreateSessionAsync(controller.OwinContext, authUser, false))
                       .Returns(Task.FromResult(0))
                       .Verifiable();
                }

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                if (shouldChallenge)
                {
                    ResultAssert.IsChallengeResult(result, enforcedProvider);
                }
                else
                {
                    ResultAssert.IsSafeRedirectTo(result, "theReturnUrl");
                    GetMock<AuthenticationService>().VerifyAll();
                }
            }

            [Fact]
            public async Task GivenNoLinkAndNoClaimData_ItDisplaysLogOnViewWithNoPrefilledData()
            {
                // Arrange
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
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
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
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
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
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
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
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
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
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
                var fakes = Get<Fakes>();
                var existingUser = new User("existingUser") { EmailAddress = "existing@example.com" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
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
                        ExternalIdentity = new ClaimsIdentity(new[] {
                            new Claim(ClaimTypes.Email, existingUser.EmailAddress)
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.True(model.External.FoundExistingUser);
                Assert.True(model.External.ExistingUserCanBeLinked);
                Assert.Equal(existingUser.EmailAddress, model.SignIn.UserNameOrEmail);
                Assert.Equal(existingUser.EmailAddress, model.Register.EmailAddress);
            }

            [Fact]
            public async Task GivenNoLinkButEmailMatchingLocalOrganizationUser_ItRejectsLinking()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var existingOrganization = new Organization("existingOrganization") { EmailAddress = "existing@example.com" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
                    cred);

                GetMock<AuthenticationService>(); // Force a mock to be created
                GetMock<IUserService>()
                    .Setup(u => u.FindByEmailAddress(existingOrganization.EmailAddress))
                    .Returns(existingOrganization);

                var controller = GetController<AuthenticationController>();

                GetMock<AuthenticationService>()
                    .Setup(x => x.AuthenticateExternalLogin(controller.OwinContext))
                    .CompletesWith(new AuthenticateExternalLoginResult()
                    {
                        ExternalIdentity = new ClaimsIdentity(new[] {
                            new Claim(ClaimTypes.Email, existingOrganization.EmailAddress)
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.True(model.External.FoundExistingUser);
                Assert.False(model.External.ExistingUserCanBeLinked);
                Assert.Equal(AssociateExternalAccountViewModel.ExistingUserLinkingErrorType.AccountIsOrganization, model.External.ExistingUserLinkingError);
            }

            [Fact]
            public async Task GivenNoLinkButEmailMatchingLocalUserWithExistingExternal_ItRejectsLinking()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var existingUser = new User("existingUser")
                {
                    EmailAddress = "existing@example.com",
                    Credentials = new[] { new Credential(CredentialTypes.External.Prefix + "foo", "externalloginvalue") }
                };

                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
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
                        ExternalIdentity = new ClaimsIdentity(new[] {
                            new Claim(ClaimTypes.Email, existingUser.EmailAddress)
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.True(model.External.FoundExistingUser);
                Assert.False(model.External.ExistingUserCanBeLinked);
                Assert.Equal(AssociateExternalAccountViewModel.ExistingUserLinkingErrorType.AccountIsAlreadyLinked, model.External.ExistingUserLinkingError);
            }

            [Fact]
            public async Task GivenNoLinkButEmailMatchingLocalAdminUserWithExistingExternal_ItAcceptsLinking()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var existingUser = new User("existingUser")
                {
                    EmailAddress = "existing@example.com",
                    Credentials = new[]
                    {
                        new Credential(CredentialTypes.External.Prefix + "foo", "externalloginvalue")
                    },
                    Roles = new[]
                    {
                        new Role { Name = CoreConstants.AdminRoleName }
                    }
                };

                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "Bloog");
                var msAuther = new MicrosoftAccountAuthenticator();
                var msaUI = msAuther.GetUI();
                var authUser = new AuthenticatedUser(
                    fakes.CreateUser("test", cred),
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
                        ExternalIdentity = new ClaimsIdentity(new[] {
                            new Claim(ClaimTypes.Email, existingUser.EmailAddress)
                        }),
                        Authenticator = msAuther
                    });

                // Act
                var result = await controller.LinkExternalAccount("theReturnUrl");

                // Assert
                var model = ResultAssert.IsView<LogOnViewModel>(result, viewName: LinkExternalViewName);
                Assert.Equal(msaUI.AccountNoun, model.External.ProviderAccountNoun);
                Assert.Null(model.External.AccountName);
                Assert.True(model.External.FoundExistingUser);
                Assert.True(model.External.ExistingUserCanBeLinked);
                Assert.Equal(existingUser.EmailAddress, model.SignIn.UserNameOrEmail);
                Assert.Equal(existingUser.EmailAddress, model.Register.EmailAddress);
            }
        }

        public class TheShouldEnforceMultiFactorAuthenticationMethod : TestContainer
        {
            [Fact]
            public void NullResultReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();

                // Act     
                var result = controller.ShouldEnforceMultiFactorAuthentication(null);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void MissingAuthenticatorReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = new AuthenticateExternalLoginResult();

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void MissingAuthenticationReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = new AuthenticateExternalLoginResult()
                {
                    Authenticator = new MicrosoftAccountAuthenticator()
                };

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void NonSupportingAuthenticatorReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult();

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void UsersWithMFANotEnabledReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult(new AzureActiveDirectoryV2Authenticator());
                externalResult.Authentication.User.EnableMultiFactorAuthentication = false;

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void IfAlreadyUsedMFAReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult(new AzureActiveDirectoryV2Authenticator());
                externalResult.LoginDetails = new ExternalLoginSessionDetails("random@email.com", usedMultiFactorAuthentication: true);

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void NonExternalCredentialReturnsfalse()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var cred = new CredentialBuilder().CreatePasswordCredential("mysecretpassword");
                var user = fakes.CreateUser("test", cred);
                user.EnableMultiFactorAuthentication = true;
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult(new AzureActiveDirectoryV2Authenticator());
                externalResult.Authentication = new AuthenticatedUser(user, cred);

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void NonMicrosoftAccountAuthetnicationReturnsFalse()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult(new AzureActiveDirectoryV2Authenticator(), "AzureActiveDirectory");

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void MicrosoftAccountCredentialReturnsTrue()
            {
                // Arrange
                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var externalResult = GetExternalResult(new AzureActiveDirectoryV2Authenticator());

                // Act
                var result = controller.ShouldEnforceMultiFactorAuthentication(externalResult);

                // Assert
                Assert.True(result);
            }

            private AuthenticateExternalLoginResult GetExternalResult(Authenticator provider = null, string credType = "MicrosoftAccount")
            {
                var fakes = Get<Fakes>();
                var email = "test@email.com";
                var cred = new CredentialBuilder().CreateExternalCredential(credType, "blorg", "Bloog");
                var user = fakes.CreateUser("test", cred);
                user.EmailAddress = email;
                user.EnableMultiFactorAuthentication = true;

                var auther = provider ?? new MicrosoftAccountAuthenticator();
                var authUser = new AuthenticatedUser(user, cred);

                return new AuthenticateExternalLoginResult()
                {
                    Authenticator = auther,
                    Authentication = authUser,
                    Credential = cred,
                    LoginDetails = new ExternalLoginSessionDetails(email, usedMultiFactorAuthentication: false)
                };
            }
        }

        public class TheShouldChallengeEnforcedProviderMethod : TestContainer
        {
            [Theory]
            [InlineData("Foo", true)]
            [InlineData("AzureActiveDirectory", false)]
            public void VerifyShouldChallenge(string providerUsedForLogin, bool shouldChallenge)
            {
                // Arrange
                var enforcedProvider = "AzureActiveDirectory";

                EnableAllAuthenticators(Get<AuthenticationService>());

                var controller = GetController<AuthenticationController>();
                var authUser = new AuthenticatedUser(
                    new User("theUsername")
                    {
                        EmailAddress = "confirmed@example.com",
                        Roles =
                        {
                            new Role { Name = CoreConstants.AdminRoleName }
                        }
                    },
                    new Credential { Type = providerUsedForLogin });

                // Act
                ActionResult challengeResult;
                var result = controller.ShouldChallengeEnforcedProvider(enforcedProvider, authUser, null, out challengeResult);

                // Assert
                Assert.Equal(shouldChallenge, result);
                if (shouldChallenge)
                {
                    ResultAssert.IsChallengeResult(challengeResult, enforcedProvider);
                }
            }
        }

        public static void VerifyExternalLinkExpiredResult(AuthenticationController controller, ActionResult result, string expectedMessage = null)
        {
            expectedMessage = expectedMessage ?? Strings.ExternalAccountLinkExpired;
            ResultAssert.IsRedirect(result, permanent: false, url: controller.Url.LogOn(relativeUrl: false));
            Assert.Equal(expectedMessage, controller.TempData["ErrorMessage"]);
        }

        private static void EnableAllAuthenticators(AuthenticationService authService)
        {
            foreach (var auther in authService.Authenticators.Values)
            {
                auther.BaseConfig.Enabled = true;

                var azureActiveDirectoryAuthenticator = auther as AzureActiveDirectoryAuthenticator;
                if (azureActiveDirectoryAuthenticator != null)
                {
                    azureActiveDirectoryAuthenticator.Config.ShowOnLoginPage = true;
                }
            }
        }
    }
}

