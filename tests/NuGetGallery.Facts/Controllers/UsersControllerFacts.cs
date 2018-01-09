// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
    {
        public static readonly int CredentialKey = 123;

        public class TheAccountAction : TestContainer
        {
            [Fact]
            public void WillGetCuratedFeedsManagedByTheCurrentUser()
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42 });

                // act
                controller.Account();

                // verify
                GetMock<ICuratedFeedService>()
                    .Verify(query => query.GetFeedsForManager(42));
            }

            [Fact]
            public void WillReturnTheAccountViewModelWithTheCuratedFeeds()
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(new User { Key = 42 });
                GetMock<ICuratedFeedService>()
                    .Setup(stub => stub.GetFeedsForManager(42))
                    .Returns(new[] { new CuratedFeed { Name = "theCuratedFeed" } });

                // act
                var model = ResultAssert.IsView<AccountViewModel>(controller.Account(), viewName: "Account");

                // verify
                Assert.Equal("theCuratedFeed", model.CuratedFeeds.First());
            }

            [Fact]
            public void LoadsDescriptionsOfCredentialsInToViewModel()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser(
                    "test",
                    credentialBuilder.CreatePasswordCredential("hunter2"),
                    TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Account();

                // Assert
                var model = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                var descs = model
                    .CredentialGroups
                    .SelectMany(x => x.Value)
                    .ToDictionary(c => c.Kind); // Should only be one of each kind
                Assert.Equal(3, descs.Count);
                Assert.Equal(Strings.CredentialType_Password, descs[CredentialKind.Password].TypeCaption);
                Assert.Equal(Strings.CredentialType_ApiKey, descs[CredentialKind.Token].TypeCaption);
                Assert.Equal(Strings.MicrosoftAccount_AccountNoun, descs[CredentialKind.External].TypeCaption);
            }


            [Fact]
            public void FiltersOutUnsupportedCredentialsInToViewModel()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();

                var credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential("v3"),
                    TestCredentialHelper.CreatePbkdf2Password("pbkdf2"),
                    TestCredentialHelper.CreateSha1Password("sha1"),
                    TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"),
                    new Credential() { Type = "unsupported" }
                };

                var user = fakes.CreateUser("test", credentials.ToArray());

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Account();

                // Assert
                var model = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                var descs = model
                    .CredentialGroups
                    .SelectMany(x => x.Value)
                    .ToDictionary(c => c.Type); // Should only be one of each type
                Assert.Equal(6, descs.Count);
                Assert.True(descs.ContainsKey(credentials[0].Type));
                Assert.True(descs.ContainsKey(credentials[1].Type));
                Assert.True(descs.ContainsKey(credentials[2].Type));
                Assert.True(descs.ContainsKey(credentials[3].Type));
                Assert.True(descs.ContainsKey(credentials[4].Type));
                Assert.True(descs.ContainsKey(credentials[5].Type));
            }
        }

        public class TheConfirmationRequiredAction : TestContainer
        {
            [Fact]
            public void WillSendNewUserEmailWhenPosted()
            {
                var user = new User
                {
                    Username = "theUsername",
                    UnconfirmedEmailAddress = "to@example.com",
                    EmailConfirmationToken = "confirmation"
                };

                string sentConfirmationUrl = null;
                MailAddress sentToAddress = null;

                GetMock<IMessageService>()
                    .Setup(m => m.SendNewAccountEmail(It.IsAny<MailAddress>(), It.IsAny<string>()))
                    .Callback<MailAddress, string>((to, url) =>
                    {
                        sentToAddress = to;
                        sentConfirmationUrl = url;
                    });

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                controller.ConfirmationRequiredPost();

                // We use a catch-all route for unit tests so we can see the parameters
                // are passed correctly.
                Assert.Equal(TestUtility.GallerySiteRootHttps + "account/confirm/theUsername/confirmation", sentConfirmationUrl);
                Assert.Equal("to@example.com", sentToAddress.Address);
            }
        }

        public class TheChangeEmailSubscriptionAction : TestContainer
        {
            [Fact]
            public async Task UpdatesEmailAllowedSetting()
            {
                var user = new User("aUsername")
                {
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailSubscriptionAsync(user, false, true))
                    .Returns(Task.CompletedTask);

                var result = await controller.ChangeEmailSubscription(new AccountViewModel
                {
                    ChangeNotifications =
                    {
                        EmailAllowed = false,
                        NotifyPackagePushed = true
                    }
                });

                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<IUserService>().Verify(u => u.ChangeEmailSubscriptionAsync(user, false, true));
            }
        }

        public class TheThanksAction : TestContainer
        {
            [Fact]
            public void ShowsDefaultThanksView()
            {
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = true;
                var controller = GetController<UsersController>();

                var result = controller.Thanks() as ViewResult;

                Assert.Empty(result.ViewName);
                Assert.Null(result.Model);
            }

            [Fact]
            public void ShowsConfirmViewWithModelWhenConfirmingEmailAddressIsNotRequired()
            {
                var configurationService = GetConfigurationService();
                configurationService.Current.ConfirmEmailAddresses = false;

                var controller = GetController<UsersController>();

                ResultAssert.IsView(controller.Thanks());
            }
        }

        public class TheForgotPasswordAction : TestContainer
        {
            [Fact]
            public async Task SendsEmailWithPasswordResetUrl()
            {
                string resetUrl = TestUtility.GallerySiteRootHttps + "account/forgotpassword/somebody/confirmation";
                var user = new User("somebody")
                {
                    EmailAddress = "some@example.com",
                    PasswordResetToken = "confirmation",
                    PasswordResetTokenExpirationDate = DateTime.UtcNow.AddHours(Constants.PasswordResetTokenExpirationHours)
                };
                GetMock<IMessageService>()
                    .Setup(s => s.SendPasswordResetInstructions(user, resetUrl, true));
                GetMock<IUserService>()
                    .Setup(s => s.FindByEmailAddress("user"))
                    .Returns(user);
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .CompletesWith(new PasswordResetResult(PasswordResetResultType.Success, user));
                var controller = GetController<UsersController>();
                var model = new ForgotPasswordViewModel { Email = "user" };

                await controller.ForgotPassword(model);

                GetMock<IMessageService>()
                    .Verify(s => s.SendPasswordResetInstructions(user, resetUrl, true));
            }

            [Fact]
            public async Task RedirectsAfterGeneratingToken()
            {
                var user = new User { EmailAddress = "some@example.com", Username = "somebody" };
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .CompletesWith(new PasswordResetResult(PasswordResetResultType.Success, user))
                    .Verifiable();
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = await controller.ForgotPassword(model) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                    .Verify(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60));
            }

            [Fact]
            public async Task ShowsErrorIfUserWasNotFound()
            {
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .ReturnsAsync(new PasswordResetResult(PasswordResetResultType.UserNotFound, user: null));
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                var result = await controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
                Assert.Contains(Strings.CouldNotFindAnyoneWithThatUsernameOrEmail, result.ViewData.ModelState["Email"].Errors.Select(e => e.ErrorMessage));
            }

            [Fact]
            public async Task ShowsErrorIfUnconfirmedAccount()
            {
                var user = new User("user") { UnconfirmedEmailAddress = "unique@example.com" };
                var authService = Get<AuthenticationService>();
                authService.Entities.Users.Add(user);

                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = user.Username };

                var result = await controller.ForgotPassword(model) as ViewResult;

                Assert.NotNull(result);
                Assert.IsNotType(typeof(RedirectResult), result);
                Assert.Contains(Strings.UserIsNotYetConfirmed, result.ViewData.ModelState["Email"].Errors.Select(e => e.ErrorMessage));
            }

            [Fact]
            public async Task ThrowsNotImplementedExceptionWhenResultTypeIsUnknown()
            {
                // Arrange
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .ReturnsAsync(new PasswordResetResult((PasswordResetResultType)(-1), user: new User()));
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                // Act & Assert
                await Assert.ThrowsAsync<NotImplementedException>(() => controller.ForgotPassword(model));
            }

            [Theory]
            [MemberData(nameof(ResultTypes))]
            public async Task NoResultsTypesThrow(PasswordResetResultType resultType)
            {
                // Arrange
                GetMock<AuthenticationService>()
                    .Setup(s => s.GeneratePasswordResetToken("user", Constants.PasswordResetTokenExpirationHours * 60))
                    .ReturnsAsync(new PasswordResetResult(resultType, user: new User()));
                var controller = GetController<UsersController>();

                var model = new ForgotPasswordViewModel { Email = "user" };

                try
                {
                    // Act 
                    await controller.ForgotPassword(model);
                }
                catch (Exception e)
                {
                    // Assert
                    Assert.True(false, $"No exception should be thrown for result type {resultType}: {e}");
                }
            }

            public static IEnumerable<object[]> ResultTypes
            {
                get
                {
                    return Enum
                        .GetValues(typeof(PasswordResetResultType))
                        .Cast<PasswordResetResultType>()
                        .Select(v => new object[] { v });
                }
            }
        }

        public class TheResetPasswordAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ShowsErrorIfTokenExpired(bool forgot)
            {
                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWithNull();
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                await controller.ResetPassword("user", "token", model, forgot);

                Assert.Equal("The Password Reset Token is not valid or expired.", controller.ModelState[""].Errors[0].ErrorMessage);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ResetsPasswordForValidToken(bool forgot)
            {
                var cred = new CredentialBuilder().CreatePasswordCredential("foo");
                cred.User = new User("foobar");

                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWith(cred);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                var result = await controller.ResetPassword("user", "token", model, forgot) as RedirectToRouteResult;

                Assert.NotNull(result);
                GetMock<AuthenticationService>()
                          .Verify(u => u.ResetPasswordWithToken("user", "token", "newpwd"));
            }

            [Fact]
            public async Task SendsPasswordAddedMessageWhenForgotFalse()
            {
                var cred = new CredentialBuilder().CreatePasswordCredential("foo");
                cred.User = new User("foobar");

                GetMock<AuthenticationService>()
                    .Setup(u => u.ResetPasswordWithToken("user", "token", "newpwd"))
                    .CompletesWith(cred);
                var controller = GetController<UsersController>();
                var model = new PasswordResetViewModel
                {
                    ConfirmPassword = "pwd",
                    NewPassword = "newpwd"
                };

                await controller.ResetPassword("user", "token", model, forgot: false);

                GetMock<IMessageService>()
                    .Verify(m => m.SendCredentialAddedNotice(cred.User, 
                                                             It.Is<CredentialViewModel>(c => c.Type == cred.Type)));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WhenModelIsInvalidItIsRetried(bool forgot)
            {
                var controller = GetController<UsersController>();

                controller.ModelState.AddModelError("test", "test");

                var result = await controller.ResetPassword("user", "token", new PasswordResetViewModel(), forgot);

                Assert.NotNull(result);
                Assert.IsType<ViewResult>(result);

                var viewResult = result as ViewResult;
                Assert.Equal(forgot, viewResult.ViewBag.ForgotPassword);
            }
        }

        public class TheConfirmAction : TestContainer
        {
            [Fact]
            public async Task ConfirmsTheUser()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Have to set this up first because it needs to return a task.
                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, "aToken"))
                    .CompletesWith(true);

                var result = await controller.Confirm("aUsername", "aToken");

                GetMock<IUserService>()
                    .Verify(u => u.ConfirmEmailAddress(user, "aToken"));
            }

            [Fact]
            public async Task ShowsAnErrorForWrongUsername()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("wrongUsername", "aToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.WrongUsername);
            }

            [Fact]
            public async Task ShowsAnErrorForWrongToken()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .CompletesWith(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("aUsername", "wrongToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
            }

            [Fact]
            public async Task ShowsAnErrorForConflictingEmailAddress()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "aToken",
                };

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, It.IsAny<string>()))
                    .Throws(new EntityException("msg"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("aUsername", "aToken");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.True(model.DuplicateEmailAddress);
            }

            [Fact]
            public async Task SendsAccountChangedNoticeWhenConfirmingChangedEmail()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };

                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.Confirm("username", "the-token");

                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(user, "old@example.com"));
            }

            [Fact]
            public async Task DoesntSendAccountChangedEmailsWhenNoOldConfirmedAddress()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = null,
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };

                GetMock<IUserService>()
                          .Setup(u => u.ConfirmEmailAddress(user, "the-token"))
                          .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // act:
                var result = await controller.Confirm("username", "the-token");

                // verify:
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.True(model.SuccessfulConfirmation);
                Assert.True(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                          .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task DoesntSendAccountChangedEmailsIfConfirmationTokenDoesntMatch()
            {
                var user = new User
                {
                    Username = "username",
                    EmailAddress = "old@example.com",
                    UnconfirmedEmailAddress = "new@example.com",
                    EmailConfirmationToken = "the-token"
                };

                GetMock<IUserService>()
                    .Setup(u => u.ConfirmEmailAddress(user, "faketoken"))
                    .CompletesWith(false);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // act:
                var result = await controller.Confirm("username", "faketoken");

                // verify:
                var model = ResultAssert.IsView<ConfirmationViewModel>(result);
                Assert.False(model.SuccessfulConfirmation);
                Assert.False(model.ConfirmingNewAccount);
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
                GetMock<IMessageService>()
                  .Verify(m => m.SendEmailChangeNoticeToPreviousEmailAddress(It.IsAny<User>(), It.IsAny<string>()), Times.Never());
            }
        }

        public class TheApiKeysAction
            : TestContainer
        {
            public static IEnumerable<object[]> CurrentUserIsInPackageOwnersWithPushNew_Data
            {
                get
                {
                    foreach (var currentUser in 
                        new[] 
                        {
                            TestUtility.FakeUser,
                            TestUtility.FakeAdminUser,
                            TestUtility.FakeOrganizationAdmin,
                            TestUtility.FakeOrganizationCollaborator
                        })
                    {
                        yield return MemberDataHelper.AsData(currentUser);
                    }
                }
            }

            [Theory]
            [MemberData(nameof(CurrentUserIsInPackageOwnersWithPushNew_Data))]
            public void CurrentUserIsFirstInPackageOwnersWithPushNew(User currentUser)
            {
                var model = GetModelForApiKeys(currentUser);

                var firstPackageOwner = model.PackageOwners.First();
                Assert.True(firstPackageOwner.Owner == currentUser.Username);
                Assert.True(firstPackageOwner.CanPushNew);
            }
            
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void OrganizationIsInPackageOwnersIfMember(bool isAdmin)
            {
                var currentUser = isAdmin ? TestUtility.FakeOrganizationAdmin : TestUtility.FakeOrganizationCollaborator;
                var organization = TestUtility.FakeOrganization;

                var model = GetModelForApiKeys(currentUser);
                
                Assert.Equal(1, model.PackageOwners.Count(o => o.Owner == organization.Username && o.CanPushNew == isAdmin));
            }

            public static IEnumerable<object[]> OrganizationIsNotInPackageOwnersIfNotMember_Data
            {
                get
                {
                    foreach (var currentUser in
                        new[]
                        {
                            TestUtility.FakeUser,
                            TestUtility.FakeAdminUser
                        })
                    {
                        yield return MemberDataHelper.AsData(currentUser);
                    }
                }
            }

            [Theory]
            [MemberData(nameof(OrganizationIsNotInPackageOwnersIfNotMember_Data))]
            public void OrganizationIsNotInPackageOwnersIfNotMember(User currentUser)
            {
                var organization = TestUtility.FakeOrganization;

                var model = GetModelForApiKeys(currentUser);

                Assert.Equal(0, model.PackageOwners.Count(o => o.Owner == organization.Username));
            }

            private ApiKeyListViewModel GetModelForApiKeys(User currentUser)
            {
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = controller.ApiKeys();

                // Assert
                Assert.IsType<ViewResult>(result);
                var viewResult = result as ViewResult;

                Assert.IsType<ApiKeyListViewModel>(viewResult.Model);
                return viewResult.Model as ApiKeyListViewModel;
            }
        }

        public class TheGenerateApiKeyAction : TestContainer
        {
            [InlineData(null)]
            [InlineData(" ")]
            [Theory]
            public async Task WhenEmptyDescriptionProvidedRedirectsToAccountPageWithError(string description)
            {
                // Arrange 
                var user = new User { Username = "the-username" };
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.GenerateApiKey(
                    description: description,
                    owner: user.Username,
                    scopes: null,
                    expirationInDays: null);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.ApiKeyDescriptionRequired) == 0);
            }

            public static IEnumerable<object[]> WhenScopeOwnerDoesNotMatch_ReturnsBadRequest_Data
            {
                get
                {
                    foreach (var getCurrentUser in 
                        new Func<Fakes, User>[] 
                        {
                            (fakes) => fakes.User,
                            (fakes) => fakes.Admin
                        })
                    {
                        yield return new object[]
                        {
                            getCurrentUser
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WhenScopeOwnerDoesNotMatch_ReturnsBadRequest_Data))]
            public Task WhenScopeOwnerDoesNotMatch_ReturnsBadRequest(Func<Fakes, User> getCurrentUser)
            {
                // Arrange 
                var fakes = new Fakes();
                var currentUser = getCurrentUser(fakes);
                var userInOwnerScope = fakes.ShaUser;

                return WhenScopeOwnerDoesNotMatch_ReturnsBadRequest(currentUser, userInOwnerScope);
            }

            private async Task WhenScopeOwnerDoesNotMatch_ReturnsBadRequest(User currentUser, User userInOwnerScope)
            {
                // Arrange 
                var user = currentUser;
                var otherUser = userInOwnerScope;
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(otherUser.Username))
                    .Returns(otherUser);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.GenerateApiKey(
                    description: "theApiKey",
                    owner: otherUser.Username,
                    scopes: new[] { NuGetScopes.PackagePush },
                    subjects: new[] { "*" },
                    expirationInDays: null);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.ApiKeyScopesNotAllowed) == 0);
            }

            [Theory]
            [InlineData(true, NuGetScopes.PackagePush)]
            [InlineData(true, NuGetScopes.PackagePushVersion)]
            [InlineData(true, NuGetScopes.PackageUnlist)]
            [InlineData(false, NuGetScopes.PackagePushVersion)]
            [InlineData(false, NuGetScopes.PackageUnlist)]
            public async Task WhenScopeOwnerMatchesOrganizationWithPermission_ReturnsSuccess(bool isAdmin, string scope)
            {
                // Arrange 
                var fakes = new Fakes();
                var user = isAdmin ? fakes.OrganizationAdmin : fakes.OrganizationCollaborator;
                var orgUser = fakes.Organization;
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(orgUser.Username))
                    .Returns(orgUser);

                GetMock<AuthenticationService>()
                    .Setup(u => u.AddCredential(It.IsAny<User>(),
                                                It.IsAny<Credential>()))
                .Callback<User, Credential>((u, c) =>
                {
                    u.Credentials.Add(c);
                    c.User = u;
                })
                .Completes()
                .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Arrange & Act
                var result = await controller.GenerateApiKey(
                    description: "theApiKey",
                    owner: orgUser.Username,
                    scopes: new[] { scope },
                    subjects: new[] { "*" },
                    expirationInDays: null);

                // Assert
                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKey.V4);
                Assert.NotNull(apiKey);
            }

            [Theory]
            [InlineData(false, NuGetScopes.PackagePush)]
            public async Task WhenScopeOwnerMatchesOrganizationWithoutPermission_ReturnsFailure(bool isAdmin, string scope)
            {
                // Arrange 
                var fakes = new Fakes();
                var user = isAdmin ? fakes.OrganizationAdmin : fakes.OrganizationCollaborator;
                var orgUser = fakes.Organization;
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(orgUser.Username))
                    .Returns(orgUser);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Arrange & Act
                var result = await controller.GenerateApiKey(
                    description: "theApiKey",
                    owner: orgUser.Username,
                    scopes: new[] { scope },
                    subjects: new[] { "*" },
                    expirationInDays: null);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.ApiKeyScopesNotAllowed) == 0);
            }

            private async Task<JsonResult> GenerateApiKeyForOrganization(bool isAdmin, string scope)
            {
                // Arrange 
                var fakes = new Fakes();
                var user = fakes.User;
                var orgUser = fakes.Organization;
                orgUser.Organizations.First().IsAdmin = isAdmin;
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(orgUser.Username))
                    .Returns(orgUser);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                return await controller.GenerateApiKey(
                    description: "theApiKey",
                    owner: orgUser.Username,
                    scopes: new[] { scope },
                    subjects: new[] { "*" },
                    expirationInDays: null);
            }

            [InlineData(180, 180)]
            [InlineData(700, 365)]
            [InlineData(-1, 365)]
            [InlineData(0, 365)]
            [InlineData(null, 365)]
            [Theory]
            public async Task WhenExpirationInDaysIsProvidedItsUsed(int? inputExpirationInDays, int expectedExpirationInDays)
            {
                // Arrange 
                var user = new User("the-username");

                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = 365;

                GetMock<AuthenticationService>()
                 .Setup(u => u.AddCredential(
                     It.IsAny<User>(),
                     It.IsAny<Credential>()))
                 .Callback<User, Credential>((u, c) =>
                 {
                     u.Credentials.Add(c);
                     c.User = u;
                 })
                 .Completes()
                 .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(user.Username))
                    .Returns(user);

                // Act
                await controller.GenerateApiKey(
                    description: "my new api key",
                    owner: user.Username,
                    scopes: new[] { NuGetScopes.PackageUnlist },
                    subjects: null,
                    expirationInDays: inputExpirationInDays);

                // Assert
                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKey.V4);

                Assert.NotNull(apiKey);
                Assert.NotNull(apiKey.Expires);
                Assert.Equal(expectedExpirationInDays, TimeSpan.FromTicks(apiKey.ExpirationTicks.Value).Days);
            }

            public static IEnumerable<object[]> CreatesNewApiKeyCredential_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            "permissions to several scopes, several packages",
                            new[] {NuGetScopes.PackageUnlist, NuGetScopes.PackagePush},
                            new[] {"abc", "def"},
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to several scopes, all packages",
                            new [] { NuGetScopes.PackageUnlist, NuGetScopes.PackagePush },
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.PackageUnlist),
                                new Scope("*", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to single scope, all packages",
                            new [] { NuGetScopes.PackageUnlist },
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.PackageUnlist)
                            }
                        },
                        new object[]
                        {
                            "permissions to everything",
                            null,
                            null,
                            new []
                            {
                                new Scope("*", NuGetScopes.All)
                            }
                        },
                        new object[]
                        {
                            "empty subjects are ignored",
                            new [] { NuGetScopes.PackageUnlist },
                            new[] {"abc", "def", string.Empty, null, "   "},
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackageUnlist)
                            }
                        }
                    };
                }
            }

            [MemberData(nameof(CreatesNewApiKeyCredential_Input))]
            [Theory]
            public async Task CreatesNewApiKeyCredential(string description, string[] scopes, string[] subjects, Scope[] expectedScopes)
            {
                // Arrange 
                var user = new User("the-username");
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(user.Username))
                    .Returns(user);

                GetMock<AuthenticationService>()
                   .Setup(u => u.AddCredential(
                       It.IsAny<User>(),
                       It.IsAny<Credential>()))
                   .Callback<User, Credential>((u, c) =>
                   {
                       u.Credentials.Add(c);
                       c.User = u;
                   })
                   .Completes()
                   .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                await controller.GenerateApiKey(
                    description: description,
                    owner: user.Username,
                    scopes: scopes,
                    subjects: subjects,
                    expirationInDays: null);

                // Assert
                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKey.V4);

                Assert.NotNull(apiKey);
                Assert.Equal(description, apiKey.Description);
                Assert.Equal(expectedScopes.Length, apiKey.Scopes.Count);

                foreach (var expectedScope in expectedScopes)
                {
                    var actualScope =
                        apiKey.Scopes.First(x => x.AllowedAction == expectedScope.AllowedAction &&
                                                 x.Subject == expectedScope.Subject);
                    Assert.NotNull(actualScope);
                }
            }

            [Fact]
            public async Task ReturnsNewCredentialJson()
            {
                // Arrange
                var user = new User { Username = "the-username" };

                var configurationService = GetConfigurationService();
                configurationService.Current.ExpirationInDaysForApiKeyV1 = 365;

                GetMock<AuthenticationService>()
                  .Setup(u => u.AddCredential(
                      It.IsAny<User>(),
                      It.IsAny<Credential>()))
                  .Callback<User, Credential>((u, c) =>
                  {
                      u.Credentials.Add(c);
                      c.User = u;
                  })
                  .Completes()
                  .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(user.Username))
                    .Returns(user);

                // Act
                var result = await controller.GenerateApiKey(
                    description: "description",
                    owner: user.Username,
                    scopes: new[] { NuGetScopes.PackageUnlist, NuGetScopes.PackagePush },
                    subjects: new[] { "a" },
                    expirationInDays: 90);

                // Assert
                var credentialViewModel = result.Data as ApiKeyViewModel;
                Assert.NotNull(credentialViewModel);

                var apiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKey.V4);

                Assert.NotEqual(apiKey.Value, credentialViewModel.Value);
                Assert.True(ApiKeyV4.TryParse(credentialViewModel.Value, out ApiKeyV4 apiKeyV4));
                Assert.True(apiKeyV4.Verify(apiKey.Value));

                Assert.Equal(apiKey.Key, credentialViewModel.Key);
                Assert.Equal(apiKey.Description, credentialViewModel.Description);
                Assert.Equal(apiKey.Expires.Value.ToString("O"), credentialViewModel.Expires);
            }

            [Fact]
            public async Task SendsNotificationMailToUser()
            {
                var user = new User { Username = "the-username" };

                GetMock<AuthenticationService>()
                  .Setup(u => u.AddCredential(
                      It.IsAny<User>(),
                      It.IsAny<Credential>()))
                  .Callback<User, Credential>((u, c) =>
                  {
                      u.Credentials.Add(c);
                      c.User = u;
                  })
                  .Completes()
                  .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(user.Username))
                    .Returns(user);


                var result = await controller.GenerateApiKey(
                    description: "description",
                    owner: user.Username,
                    scopes: new[] { NuGetScopes.PackageUnlist, NuGetScopes.PackagePush },
                    subjects: new[] { "a" },
                    expirationInDays: 90);

                GetMock<IMessageService>()
                    .Verify(m => m.SendCredentialAddedNotice(user, It.IsAny<CredentialViewModel>()));
            }
        }

        public class TheChangeEmailAction : TestContainer
        {
            [Fact]
            public async Task DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "old@example.com",
                    Key = 1,
                };

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success,
                                                    new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate(It.IsAny<string>(), It.IsAny<string>()))
                    .CompletesWith(authResult);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Throws(new EntityException("msg"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var result = await controller.ChangeEmail(
                    new AccountViewModel()
                    {
                        ChangeEmail = new ChangeEmailViewModel
                        {
                            NewEmail = "new@example.com"
                        }
                    });
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal("msg", controller.ModelState["ChangeEmail.NewEmail"].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task SendsEmailChangeConfirmationNoticeWhenChangingAConfirmedEmailAddress()
            {
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "test@example.com",
                    EmailAllowed = true
                };

                var authResult =
                    new PasswordAuthenticationResult(
                        PasswordAuthenticationResult.AuthenticationResult.Success,
                        new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("theUsername", "password"))
                    .CompletesWith(authResult);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(user, "new@example.com"))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "token"))
                    .Completes();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                var result = await controller.ChangeEmail(model);

                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()));
            }

            [Fact]
            public async Task DoesNotSendEmailChangeConfirmationNoticeWhenAddressDoesntChange()
            {
                var user = new User
                {
                    EmailAddress = "old@example.com",
                    Username = "aUsername",
                };

                var authResult =
                    new PasswordAuthenticationResult(PasswordAuthenticationResult.AuthenticationResult.Success, new AuthenticatedUser(user, new Credential()));

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("aUsername", "password"))
                    .CompletesWith(authResult);
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback(() => user.UpdateEmailAddress("old@example.com", () => "new-token"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "old@example.com",
                        Password = "password"
                    }
                };

                await controller.ChangeEmail(model);

                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "old@example.com"), Times.Never());
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task DoesNotSendEmailChangeConfirmationNoticeWhenUserWasNotConfirmed()
            {
                var user = new User
                {
                    Username = "aUsername",
                    UnconfirmedEmailAddress = "old@example.com",
                };

                GetMock<AuthenticationService>()
                    .Setup(u => u.Authenticate("aUsername", "password"))
                    .CompletesWith(new PasswordAuthenticationResult(
                        PasswordAuthenticationResult.AuthenticationResult.Success, new AuthenticatedUser(user, new Credential())));
                GetMock<IUserService>()
                    .Setup(u => u.ChangeEmailAddress(It.IsAny<User>(), It.IsAny<string>()))
                    .Callback(() => user.UpdateEmailAddress("new@example.com", () => "new-token"))
                    .Completes();
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel()
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                await controller.ChangeEmail(model);

                Assert.Equal("Your new email address was saved!", controller.TempData["Message"]);
                GetMock<IUserService>()
                    .Verify(u => u.ChangeEmailAddress(user, "new@example.com"));
                GetMock<IMessageService>()
                    .Verify(m => m.SendEmailChangeConfirmationNotice(It.IsAny<MailAddress>(), It.IsAny<string>()), Times.Never());
            }

            [Fact]
            public async Task WhenPasswordValidationFailsErrorIsReturned()
            {
                // Arrange
                var user = new User
                {
                    Username = "theUsername",
                    EmailAddress = "test@example.com",
                    Credentials = new[] { new Credential(CredentialTypes.Password.V3, "abc") }
                };

                Credential credential;
                GetMock<AuthenticationService>()
                    .Setup(u => u.ValidatePasswordCredential(It.IsAny<IEnumerable<Credential>>(), It.IsAny<string>(), out credential))
                    .Returns(false);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var model = new AccountViewModel
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                var result = await controller.ChangeEmail(model);

                Assert.IsType<ViewResult>(result);
                Assert.IsType<AccountViewModel>(((ViewResult)result).Model);
            }
        }

        public class TheChangePasswordAction : TestContainer
        {
            [Fact]
            public async Task GivenInvalidView_ItReturnsView()
            {
                // Arrange
                var controller = GetController<UsersController>();
                controller.ModelState.AddModelError("ChangePassword.blarg", "test");
                var inputModel = new AccountViewModel
                {
                    ChangePassword =
                    {
                        EnablePasswordLogin = true,
                    }
                };
                controller.SetCurrentUser(new User()
                {
                    Credentials = new List<Credential> {
                        new CredentialBuilder().CreatePasswordCredential("abc")
                    }
                });

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                Assert.Same(inputModel, outputModel);
            }

            [Fact]
            public async Task GivenMismatchedNewPassword_ItAddsModelError()
            {
                // Arrange
                var user = new User("foo");
                user.Credentials.Add(new CredentialBuilder().CreatePasswordCredential("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new"))
                    .CompletesWith(false);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        EnablePasswordLogin = true,
                        OldPassword = "old",
                        NewPassword = "new",
                        VerifyPassword = "new2",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                Assert.Same(inputModel, outputModel);
                Assert.NotEqual(inputModel.ChangePassword.NewPassword, inputModel.ChangePassword.VerifyPassword);

                var errorMessages = controller
                    .ModelState["ChangePassword.VerifyPassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.PasswordDoesNotMatch });
            }

            [Fact]
            public async Task GivenFailureInAuthService_ItAddsModelError()
            {
                // Arrange
                var user = new User("foo");
                user.Credentials.Add(new CredentialBuilder().CreatePasswordCredential("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new"))
                    .CompletesWith(false);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        EnablePasswordLogin = true,
                        OldPassword = "old",
                        NewPassword = "new",
                        VerifyPassword = "new",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<AccountViewModel>(result, viewName: "Account");
                Assert.Same(inputModel, outputModel);

                var errorMessages = controller
                    .ModelState["ChangePassword.OldPassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.CurrentPasswordIncorrect });
            }

            [Fact]
            public async Task GivenDisabledPasswordLogin_RemovesCredentialAndSendsNotice()
            {
                // Arrange
                var user = new User("foo");
                var cred = new CredentialBuilder().CreatePasswordCredential("old");
                user.Credentials.Add(cred);
                user.Credentials.Add(new CredentialBuilder()
                    .CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(m => 
                                m.SendCredentialRemovedNotice(
                                    user,
                                    It.Is<CredentialViewModel>(c => c.Type == CredentialTypes.ExternalPrefix + "MicrosoftAccount")))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        EnablePasswordLogin = false,
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.PasswordRemoved, controller.TempData["Message"]);
            }

            [Fact]
            public async Task GivenSuccessInAuthService_ItRedirectsBackToManageCredentialsWithMessage()
            {
                // Arrange
                var user = new User("foo");
                user.Credentials.Add(new CredentialBuilder().CreatePasswordCredential("old"));

                GetMock<AuthenticationService>()
                    .Setup(u => u.ChangePassword(user, "old", "new"))
                    .CompletesWith(true);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                var inputModel = new AccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        EnablePasswordLogin = true,
                        OldPassword = "old",
                        NewPassword = "new",
                        VerifyPassword = "new",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.PasswordChanged, controller.TempData["Message"]);
            }

            [Fact]
            public async Task GivenNoOldPassword_ItSendsAPasswordSetEmail()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test");
                user.EmailAddress = "confirmed@example.com";

                GetMock<AuthenticationService>()
                    .Setup(a => a.GeneratePasswordResetToken(user, It.IsAny<int>()))
                    .ReturnsAsync(PasswordResetResultType.Success)
                    .Callback<User, int>((u, _) => u.PasswordResetToken = "t0k3n");

                string actualConfirmUrl = null;
                GetMock<IMessageService>()
                    .Setup(a => a.SendPasswordResetInstructions(user, It.IsAny<string>(), false))
                    .Callback<User, string, bool>((_, url, __) => actualConfirmUrl = url)
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                await controller.ChangePassword(new AccountViewModel());

                // Assert
                Assert.Equal(TestUtility.GallerySiteRootHttps + "account/setpassword/test/t0k3n", actualConfirmUrl);
                GetMock<IMessageService>().VerifyAll();
            }

            [Fact]
            public async Task GivenNoOldPasswordForUnconfirmedAccount_ItAddsModelError()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test");
                user.UnconfirmedEmailAddress = "unconfirmed@example.com";
                GetMock<AuthenticationService>()
                    .Setup(a => a.GeneratePasswordResetToken(user, It.IsAny<int>()))
                    .ReturnsAsync(PasswordResetResultType.UserNotConfirmed);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                await controller.ChangePassword(new AccountViewModel());

                // Assert
                var errorMessages = controller
                    .ModelState["ChangePassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.UserIsNotYetConfirmed });
            }
        }

        public class TheRemovePasswordAction : TestContainer
        {
            [Fact]
            public async Task GivenNoOtherLoginCredentials_ItRedirectsBackWithAnErrorMessage()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreatePasswordCredential("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CannotRemoveOnlyLoginCredential, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenNoPassword_ItRedirectsBackWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CredentialNotFound, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreatePasswordCredential("password");
                var user = fakes.CreateUser("test",
                    cred,
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog"));

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(m => m.SendCredentialRemovedNotice(
                                    user,
                                    It.Is<CredentialViewModel>(c => c.Type == cred.Type)))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemovePassword();

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
            }
        }

        public class TheRemoveCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenNoOtherLoginCredentials_ItRedirectsBackWithAnErrorMessage()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = fakes.CreateUser("test", cred);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: cred.Type,
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CannotRemoveOnlyLoginCredential, controller.TempData["Message"]);
                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenNoCredential_ErrorIsReturnedWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreatePasswordCredential("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: CredentialTypes.ExternalPrefix + "MicrosoftAccount",
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                Assert.Equal(Strings.CredentialNotFound, controller.TempData["Message"]);

                Assert.Equal(1, user.Credentials.Count);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1)]
            [InlineData(CredentialTypes.ApiKey.V2)]
            [InlineData(CredentialTypes.ApiKey.V4)]
            public async Task GivenNoApiKeyCredential_ErrorIsReturnedWithNoChangesMade(string apiKeyType)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreatePasswordCredential("password"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: apiKeyType,
                    credentialKey: null);

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
                Assert.IsType<JsonResult>(result);
                Assert.True(string.Compare((string)((JsonResult)result).Data, Strings.CredentialNotFound) == 0);

                Assert.Equal(1, user.Credentials.Count);
            }

            [Fact]
            public async Task GivenValidRequest_ItRemovesCredAndSendsNotificationToUser()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var fakes = Get<Fakes>();
                var cred = credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blorg", "bloog");
                var user = fakes.CreateUser("test",
                    cred,
                    credentialBuilder.CreatePasswordCredential("password"));

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                    .Completes()
                    .Verifiable();
                GetMock<IMessageService>()
                    .Setup(m => 
                                m.SendCredentialRemovedNotice(
                                    user,
                                    It.Is<CredentialViewModel>(c => c.Type == CredentialTypes.ExternalPrefix + "MicrosoftAccount")))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RemoveCredential(
                    credentialType: cred.Type,
                    credentialKey: null);

                // Assert
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                GetMock<AuthenticationService>().VerifyAll();
                GetMock<IMessageService>().VerifyAll();
            }

            [Fact]
            public async Task GivenValidRequest_CanDeleteMicrosoftAccountWithMultipleMicrosoftAccounts()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var creds = new Credential[5];
                for (int i = 0; i < creds.Length; i++)
                {
                    creds[i] = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "blorg", "bloog" + i);
                    creds[i].Key = i + 1;
                }

                var user = fakes.CreateUser("test", creds);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                Assert.Equal(creds.Length, user.Credentials.Count);

                for (int i = 0; i < creds.Length - 1; i++)
                {
                    // Act
                    var result = await controller.RemoveCredential(
                        credentialType: creds[i].Type,
                        credentialKey: creds[i].Key);

                    // Assert
                    ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
                    Assert.Equal(Strings.CredentialRemoved, controller.TempData["Message"]);
                    Assert.Equal(creds.Length - i - 1, user.Credentials.Count);
                }
            }
        }

        public class TheRegenerateCredentialAction : TestContainer
        {
            [Fact]
            public async Task GivenNoCredential_ErrorIsReturnedWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var user = fakes.CreateUser("test",
                    new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1), out string plaintextApiKey));
                var cred = user.Credentials.First();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.CredentialNotFound) == 0);

                Assert.Equal(1, user.Credentials.Count);
                Assert.True(user.Credentials.Contains(cred));
            }

            public static IEnumerable<object[]> GivenANonScopedApiKeyCredential_ReturnsUnsupported_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1))
                        },
                        new object[]
                        {
                            TestCredentialHelper.CreateExternalCredential("abc")
                        },
                        new object[]
                        {
                            TestCredentialHelper.CreateSha1Password("abcd")
                        }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(GivenANonScopedApiKeyCredential_ReturnsUnsupported_Input))]
            public async Task GivenANonScopedApiKeyCredential_ReturnsUnsupported(Credential credential)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", credential);
                credential.Key = 1;

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: credential.Type,
                    credentialKey: credential.Key);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.Unsupported) == 0);
            }

            public static IEnumerable<object[]> GivenValidRequest_ItGeneratesNewCredAndRemovesOldCredAndSendsNotificationToUser_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            "permissions to several scopes, several packages",
                            new []
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            "permissions to everything",
                            new []
                            {
                                new Scope(null, NuGetScopes.All)
                            }
                        }
                    };
                }
            }

            [MemberData(nameof(GivenValidRequest_ItGeneratesNewCredAndRemovesOldCredAndSendsNotificationToUser_Input))]
            [Theory]
            public async Task GivenValidRequest_ItGeneratesNewCredAndRemovesOldCredAndSendsNotificationToUser(
                string description, Scope[] scopes)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var apiKey = new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1), out string plaintextApiKey);
                apiKey.Description = description;
                apiKey.Scopes = scopes;
                apiKey.Expires -= TimeSpan.FromDays(1);

                var user = fakes.CreateUser("test", apiKey);
                var cred = user.Credentials.First();
                cred.Key = CredentialKey;

                GetMock<AuthenticationService>()
                    .Setup(u => u.AddCredential(
                        user,
                        It.Is<Credential>(c => c.Type == CredentialTypes.ApiKey.V4)))
                    .Callback<User, Credential>((u, c) =>
                    {
                        u.Credentials.Add(c);
                        c.User = u;
                    })
                    .Completes()
                    .Verifiable();

                GetMock<AuthenticationService>()
                    .Setup(a => a.RemoveCredential(user, cred))
                     .Callback<User, Credential>((u, c) => u.Credentials.Remove(c))
                    .Completes()
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.RegenerateCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey);

                // Assert
                var viewModel = result.Data as ApiKeyViewModel;

                Assert.NotNull(viewModel);

                GetMock<AuthenticationService>().VerifyAll();

                var newApiKey = user.Credentials.FirstOrDefault(x => x.Type == CredentialTypes.ApiKey.V4);

                // Verify the ApiKey in the view model can be authenticated using the value in the DB
                Assert.NotNull(newApiKey);
                Assert.NotEqual(newApiKey.Value, viewModel.Value);
                Assert.True(ApiKeyV4.TryParse(viewModel.Value, out ApiKeyV4 apiKeyV4));
                Assert.True(apiKeyV4.Verify(newApiKey.Value));
                
                Assert.Equal(newApiKey.Key, viewModel.Key);
                Assert.Equal(description, viewModel.Description);
                Assert.Equal(newApiKey.Expires.Value.ToString("O"), viewModel.Expires);

                Assert.Equal(description, newApiKey.Description);
                Assert.Equal(scopes.Length, newApiKey.Scopes.Count);
                Assert.True(newApiKey.Expires > DateTime.UtcNow);

                foreach (var expectedScope in scopes)
                {
                    var actualScope =
                        newApiKey.Scopes.First(x => x.AllowedAction == expectedScope.AllowedAction &&
                                                 x.Subject == expectedScope.Subject);
                    Assert.NotNull(actualScope);
                }
            }
        }

        public class TheEditCredentialAction : TestContainer
        {
            public static IEnumerable<object[]> GivenANonApiKeyV2Credential_ReturnsUnsupported_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1))
                        },
                        new object[]
                        {
                            TestCredentialHelper.CreateExternalCredential("abc")
                        },
                        new object[]
                        {
                            TestCredentialHelper.CreateSha1Password("abcd")
                        }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(GivenANonApiKeyV2Credential_ReturnsUnsupported_Input))]
            public async Task GivenANonApiKeyV2Credential_ReturnsUnsupported(Credential credential)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("test", credential);
                credential.Key = 1;

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.EditCredential(
                    credentialType: credential.Type,
                    credentialKey: credential.Key,
                    subjects: new[] { "a", "b" });

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.CompareOrdinal((string)result.Data, Strings.Unsupported) == 0);
            }

            [Fact]
            public async Task GivenNoCredential_ErrorIsReturnedWithNoChangesMade()
            {
                // Arrange
                var fakes = Get<Fakes>();

                var user = fakes.CreateUser("test", new CredentialBuilder().CreateApiKey(TimeSpan.FromHours(1), out string plaintextApiKey));
                var cred = user.Credentials.First();

                var authenticationService = GetMock<AuthenticationService>();
                authenticationService
                    .Setup(x => x.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.EditCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey,
                    subjects: new[] { "a", "b" });

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, controller.Response.StatusCode);
                Assert.True(String.CompareOrdinal((string)result.Data, Strings.CredentialNotFound) == 0);

                authenticationService.Verify(x => x.EditCredentialScopes(It.IsAny<User>(), It.IsAny<Credential>(), It.IsAny<ICollection<Scope>>()), Times.Never);
            }

            public static IEnumerable<object[]> GivenValidRequest_ItEditsCredential_Input
            {
                get
                {
                    return new[]
                    {
                        new object[]
                        {
                            new [] // Removal of subjects
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            },
                            new [] { "def" },
                            new []
                            {
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            },
                        },
                        new object[]
                        {
                            new [] // Addition of subjects
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                            },
                            new [] { "abc", "def" },
                            new []
                            {
                               new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush),
                                new Scope("def", NuGetScopes.PackageUnlist),
                                new Scope("def", NuGetScopes.PackagePush)
                            }
                        },
                        new object[]
                        {
                            new [] // No subjects
                            {
                                new Scope("abc", NuGetScopes.PackageUnlist),
                                new Scope("abc", NuGetScopes.PackagePush)
                            },
                            new string[] {},
                            new []
                            {
                               new Scope("*", NuGetScopes.PackageUnlist),
                               new Scope("*", NuGetScopes.PackagePush)
                            }
                        },
                    };
                }
            }

            [MemberData(nameof(GivenValidRequest_ItEditsCredential_Input))]
            [Theory]
            public async Task GivenValidRequest_ItEditsCredential(Scope[] existingScopes, string[] modifiedSubjects, Scope[] expectedScopes)
            {
                // Arrange
                const string description = "description";
                var fakes = Get<Fakes>();
                var credentialBuilder = new CredentialBuilder();
                var apiKey = credentialBuilder.CreateApiKey(TimeSpan.FromHours(1), out string plaintextApiKey1);
                apiKey.Description = description;
                apiKey.Scopes = existingScopes;

                var apiKeyExpirationTime = apiKey.Expires;
                var apiKeyValue = apiKey.Value;


                var user = fakes.CreateUser("test", apiKey, credentialBuilder.CreateApiKey(null, out string plaintextApiKey2));
                var cred = user.Credentials.First();
                cred.Key = CredentialKey;

                GetMock<AuthenticationService>()
                   .Setup(a => a.EditCredentialScopes(user, cred, It.IsAny<ICollection<Scope>>()))
                   .Callback<User, Credential, ICollection<Scope>>((u, cr, scs) =>
                    {
                        cr.Scopes = scs;
                    })
                   .Completes()
                   .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.EditCredential(
                    credentialType: cred.Type,
                    credentialKey: CredentialKey,
                    subjects: modifiedSubjects);

                // Assert
                GetMock<AuthenticationService>().Verify(x => x.EditCredentialScopes(user, apiKey, It.IsAny<ICollection<Scope>>()), Times.Once);

                // Check return value
                var viewModel = result.Data as ApiKeyViewModel;
                Assert.NotNull(viewModel);

                Assert.Null(viewModel.Value);
                Assert.Equal(description, viewModel.Description);
                Assert.Equal(expectedScopes.Select(s => s.AllowedAction).Distinct().Count(), viewModel.Scopes.Count);

                foreach (var expectedScope in expectedScopes)
                {
                    var expectedAction = NuGetScopes.Describe(expectedScope.AllowedAction);
                    var actualScope = viewModel.Scopes.First(x => x == expectedAction);
                    Assert.NotNull(actualScope);
                }

                // Check edited value
                Assert.Equal(expectedScopes.Length, apiKey.Scopes.Count);
                Assert.Equal(apiKeyExpirationTime, apiKey.Expires); // Expiration time wasn't modified by edit
                Assert.Equal(description, apiKey.Description);  // Description wasn't modified
                Assert.Equal(apiKeyValue, apiKey.Value); // Value wasn't modified

                foreach (var expectedScope in expectedScopes)
                {
                    var actualScope =
                        apiKey.Scopes.First(x => x.AllowedAction == expectedScope.AllowedAction &&
                                                 x.Subject == expectedScope.Subject);
                    Assert.NotNull(actualScope);
                }
            }
        }

        public class TheDeleteAccountAction : TestContainer
        {
            [Fact]
            public void DeleteNotExistentAccount()
            {
                // Arrange
                var controller = GetController<UsersController>();

                // Act
                var result = controller.Delete(accountName: "NotFoundUser");

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, (int)((HttpNotFoundResult)result).StatusCode);
            }

            [Fact]
            public void DeleteDeletedAccount()
            {
                // Arrange
                string userName = "DeletedUser";
                var controller = GetController<UsersController>();

                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.IsDeleted = true;

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName))
                    .Returns(testUser);

                // act
                var result = controller.Delete(accountName: userName);

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, (int)((HttpNotFoundResult)result).StatusCode);
            }

            [Fact]
            public void DeleteHappyAccount()
            {
                // Arrange
                string userName = "DeletedUser";
                var controller = GetController<UsersController>();
                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.IsDeleted = false;

                PackageRegistration packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(testUser);

                Package userPackage = new Package()
                {
                    Description = "TestPackage",
                    Key = 1,
                    Version = "1.0.0",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(userPackage);

                List<Package> userPackages = new List<Package>() { userPackage };

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);

                // act
                var model = ResultAssert.IsView<DeleteUserAccountViewModel>(controller.Delete(accountName: userName), viewName: "DeleteUserAccount");

                // Assert
                Assert.Equal(userName, model.AccountName);
                Assert.Equal<int>(1, model.Packages.Count());
            }
        }

        public class TheDeleteAccountRequestAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DeleteAccountRequestView(bool withPendingIssues)
            {
                // Arrange
                string userName = "DeletedUser";
                string emailAddress = $"{userName}@coldmail.com";
                int userKey = 1;

                var controller = GetController<UsersController>();
                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.EmailAddress = emailAddress;
                testUser.Key = userKey;
                testUser.IsDeleted = false;

                controller.SetCurrentUser(testUser);
                PackageRegistration packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(testUser);

                Package userPackage = new Package()
                {
                    Description = "TestPackage",
                    Key = 1,
                    Version = "1.0.0",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(userPackage);

                List<Package> userPackages = new List<Package>() { userPackage };
                List<Issue> issues = new List<Issue>();
                if (withPendingIssues)
                {
                    issues.Add(new Issue()
                    {
                        IssueTitle = Strings.AccountDelete_SupportRequestTitle,
                        OwnerEmail = emailAddress,
                        CreatedBy = userName,
                        UserKey = 1,
                        IssueStatus = new IssueStatus() { Key = IssueStatusKeys.New, Name = "OneIssue" }
                    });
                }

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<ISupportRequestService>()
                   .Setup(stub => stub.GetIssues(null, null, null, null))
                   .Returns(issues);

                // act
                var result = controller.DeleteRequest() as ViewResult;
                var model = (DeleteAccountViewModel)result.Model;

                // Assert
                Assert.Equal(userName, model.AccountName);
                Assert.Equal<int>(1, model.Packages.Count());
                Assert.Equal<bool>(true, model.HasOrphanPackages);
                Assert.Equal<bool>(withPendingIssues, model.HasPendingRequests);
            }
        }

        public class TheRequestAccountDeletionAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task RequestDeleteAccountAsync(bool successOnSentRequest)
            {
                // Arrange
                string userName = "DeletedUser";
                string emailAddress = $"{userName}@coldmail.com";

                var controller = GetController<UsersController>();

                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.EmailAddress = emailAddress;
                controller.SetCurrentUser(testUser);

                List<Package> userPackages = new List<Package>();
                List<Issue> issues = new List<Issue>();

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<ISupportRequestService>()
                   .Setup(stub => stub.GetIssues(null, null, null, userName))
                   .Returns(issues);
                GetMock<ISupportRequestService>()
                  .Setup(stub => stub.AddNewSupportRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), testUser, null))
                  .ReturnsAsync(successOnSentRequest ? new Issue() : (Issue)null);

                // act
                var result = await controller.RequestAccountDeletion() as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal<string>("DeleteRequest", (string)result.RouteValues["action"]);
                bool tempData = controller.TempData.ContainsKey("RequestFailedMessage");
                Assert.Equal<bool>(!successOnSentRequest, tempData);
            }
        }

        public class TheTransformToOrganizationActionBase : TestContainer
        {
            protected UsersController CreateController(string accountToTransform, string canTransformErrorReason = "")
            {
                var configurationService = GetConfigurationService();
                configurationService.Current.OrganizationsEnabledForDomains = new string[] { "example.com" };

                var controller = GetController<UsersController>();
                var currentUser = new User(accountToTransform) { EmailAddress = $"{accountToTransform}@example.com" };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername("OrgAdmin"))
                    .Returns(new User("OrgAdmin")
                    {
                        EmailAddress = "orgadmin@example.com"
                    });

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(s => s.RequestTransformToOrganizationAccount(It.IsAny<User>(), It.IsAny<User>()))
                    .Callback<User, User>((acct, admin) => {
                        acct.OrganizationMigrationRequest = new OrganizationMigrationRequest()
                        {
                            NewOrganization = acct,
                            AdminUser = admin,
                            ConfirmationToken = "X",
                            RequestDate = DateTime.UtcNow
                        };
                    })
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                return controller;
            }
        }

        public class TheGetTransformToOrganizationAction : TheTransformToOrganizationActionBase
        {
            [Fact]
            public void WhenCanTransformReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, canTransformErrorReason: "error");

                // Act
                var result = controller.TransformToOrganization();

                // Assert
                Assert.NotNull(result);
                Assert.Equal("error", controller.TempData["TransformError"]);
            }
        }

        public class ThePostTransformToOrganizationAction : TheTransformToOrganizationActionBase
        {
            [Fact]
            public async Task WhenCanTransformReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, canTransformErrorReason: "error");

                // Act
                var result = await controller.TransformToOrganization(new TransformAccountViewModel() {
                    AdminUsername = "OrgAdmin"
                });

                // Assert
                Assert.NotNull(result);
                Assert.Equal("error", controller.TempData["TransformError"]);
            }

            [Fact]
            public async Task WhenAdminIsNotFound_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform);

                // Act
                var result = await controller.TransformToOrganization(new TransformAccountViewModel()
                {
                    AdminUsername = "AdminThatDoesNotExist"
                });

                // Assert
                Assert.NotNull(result);
                Assert.Equal(1, controller.ModelState["AdminUsername"].Errors.Count);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture,
                        Strings.TransformAccount_AdminAccountDoesNotExist, "AdminThatDoesNotExist"),
                    controller.ModelState["AdminUsername"].Errors.First().ErrorMessage);
            }

            [Fact]
            public async Task WhenValid_CreatesRequestAndRedirects()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform);

                // Act
                var result = await controller.TransformToOrganization(new TransformAccountViewModel()
                {
                    AdminUsername = "OrgAdmin"
                });

                // Assert
                Assert.IsType<RedirectResult>(result);
            }
        }
        
        public class TheConfirmTransformToOrganizationAction : TestContainer
        {
            [Fact]
            public async Task WhenAdminIsNotConfirmed_ShowsError()
            {
                // Arrange
                var controller = GetController<UsersController>();
                var currentUser = new User() { UnconfirmedEmailAddress = "unconfirmed@example.com" };
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.ConfirmTransformToOrganization("account", "token");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(Strings.TransformAccount_NotConfirmed, controller.TempData["TransformError"]);
            }

            [Fact]
            public async Task WhenAccountToTransformIsNotFound_ShowsError()
            {
                // Arrange
                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.ConfirmTransformToOrganization("account", "token");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture, Strings.TransformAccount_OrganizationAccountDoesNotExist, "account"),
                    controller.TempData["TransformError"]);
            }

            [Fact]
            public async Task WhenCanTransformReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, canTransformErrorReason: "error");

                // Act
                var result = await controller.ConfirmTransformToOrganization(accountToTransform, "token");

                // Assert
                Assert.NotNull(result);
                Assert.Equal("error", controller.TempData["TransformError"]);
            }

            [Fact]
            public async Task WhenUserServiceReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, success: false);

                // Act
                var result = await controller.ConfirmTransformToOrganization(accountToTransform, "token");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture,
                        Strings.TransformAccount_Failed, "account"),
                    controller.TempData["TransformError"]);
            }

            [Fact]
            public async Task WhenUserServiceReturnsSuccess_Redirects()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, success: true);

                // Act
                var result = await controller.ConfirmTransformToOrganization(accountToTransform, "token");

                // Assert
                Assert.NotNull(result);
                Assert.False(controller.TempData.ContainsKey("TransformError"));
            }

            private UsersController CreateController(string accountToTransform, string canTransformErrorReason = "", bool success = true)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                configurationService.Current.OrganizationsEnabledForDomains = new string[] { "example.com" };

                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(accountToTransform))
                    .Returns(new User(accountToTransform)
                    {
                        EmailAddress = $"{accountToTransform}@example.com"
                    });

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(s => s.TransformUserToOrganization(It.IsAny<User>(), It.IsAny<User>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(success));

                return controller;
            }
        }
    }
}

