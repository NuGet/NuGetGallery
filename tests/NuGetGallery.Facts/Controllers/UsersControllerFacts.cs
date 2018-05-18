﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery
{
    public class UsersControllerFacts
        : AccountsControllerFacts<UsersController, User, UserAccountViewModel>
    {
        public static readonly int CredentialKey = 123;

        private static Func<Fakes, User> _getFakesUser = (Fakes fakes) => fakes.User;

        public class TheAccountAction : TheAccountBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            protected override ActionResult InvokeAccount(UsersController controller)
            {
                return controller.Account();
            }

            [Fact]
            public void LoadsDescriptionsOfCredentialsInToViewModel()
            {
                // Arrange
                var credentialBuilder = new CredentialBuilder();
                var user = Fakes.CreateUser(
                    "test",
                    credentialBuilder.CreatePasswordCredential("hunter2"),
                    TestCredentialHelper.CreateV1ApiKey(Guid.NewGuid(), Fakes.ExpirationForApiKeyV1),
                    credentialBuilder.CreateExternalCredential("MicrosoftAccount", "blarg", "Bloog"));
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Account();

                // Assert
                var model = ResultAssert.IsView<UserAccountViewModel>(result, viewName: "Account");
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

                var user = Fakes.CreateUser("test", credentials.ToArray());

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Account();

                // Assert
                var model = ResultAssert.IsView<UserAccountViewModel>(result, viewName: "Account");
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

        public class TheCancelChangeEmailAction : TheCancelChangeEmailBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            // Note general account tests are in the base class. User-specific tests are below.
        }

        public class TheChangeEmailAction : TheChangeEmailBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            // Note general account tests are in the base class. User-specific tests are below.

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

                var model = new UserAccountViewModel
                {
                    ChangeEmail = new ChangeEmailViewModel
                    {
                        NewEmail = "new@example.com",
                        Password = "password"
                    }
                };

                // Act
                var result = await controller.ChangeEmail(model);

                // Assert
                Assert.IsType<ViewResult>(result);
                Assert.IsType<UserAccountViewModel>(((ViewResult)result).Model);
            }
        }

        public class TheConfirmationRequiredAction : TheConfirmationRequiredBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            // Note general account tests are in the base class. User-specific tests are below.
        }

        public class TheConfirmationRequiredPostAction : TheConfirmationRequiredPostBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            // Note general account tests are in the base class. User-specific tests are below.
        }

        public class TheChangeEmailSubscriptionAction : TheChangeEmailSubscriptionBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            public static IEnumerable<object[]> UpdatesEmailPreferences_Data => MemberDataHelper.Combine(AllowedCurrentUsers_Data, UpdatesEmailPreferences_DefaultData);

            // Note general account tests are in the base class. User-specific tests are below.
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
                Assert.Contains(Strings.CouldNotFindAnyoneWithThatUsernameOrEmail, result.ViewData.ModelState[string.Empty].Errors.Select(e => e.ErrorMessage));
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
                Assert.Contains(Strings.UserIsNotYetConfirmed, result.ViewData.ModelState[string.Empty].Errors.Select(e => e.ErrorMessage));
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

        public class TheConfirmAction : TheConfirmBaseAction
        {
            public static IEnumerable<object[]> AllowedCurrentUsers_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(_getFakesUser);
                }
            }

            // Note general account tests are in the base class. User-specific tests are below.
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
                Assert.True(firstPackageOwner.CanPushExisting);
                Assert.True(firstPackageOwner.CanUnlist);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public void OrganizationIsInPackageOwnersIfMember(bool isAdmin)
            {
                var currentUser = isAdmin ? TestUtility.FakeOrganizationAdmin : TestUtility.FakeOrganizationCollaborator;
                var organization = TestUtility.FakeOrganization;

                var model = GetModelForApiKeys(currentUser);

                var owner = model.PackageOwners.Single(o => o.Owner == organization.Username);
                Assert.True(owner.CanPushNew);
                Assert.True(owner.CanPushExisting);
                Assert.True(owner.CanUnlist);
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
            public async Task WhenScopeOwnerDoesNotMatch_ReturnsBadRequest(Func<Fakes, User> getCurrentUser)
            {
                // Arrange 
                var fakes = new Fakes();
                var currentUser = getCurrentUser(fakes);
                var userInOwnerScope = fakes.ShaUser;

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(userInOwnerScope.Username, false))
                    .Returns(userInOwnerScope);

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.GenerateApiKey(
                    description: "theApiKey",
                    owner: userInOwnerScope.Username,
                    scopes: new[] { NuGetScopes.PackagePush },
                    subjects: new[] { "*" },
                    expirationInDays: null);

                // Assert
                Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                Assert.True(string.Compare((string)result.Data, Strings.ApiKeyScopesNotAllowed) == 0);
            }

            public static IEnumerable<object[]> WhenScopeOwnerMatchesOrganizationWithPermission_ReturnsSuccess_Data
            {
                get
                {
                    foreach (var isAdmin in new[] { false, true })
                    {
                        foreach (var scope in new[] {
                            NuGetScopes.All,
                            NuGetScopes.PackagePush,
                            NuGetScopes.PackagePushVersion,
                            NuGetScopes.PackageUnlist,
                            NuGetScopes.PackageVerify })
                        {
                            yield return MemberDataHelper.AsData(isAdmin, scope);
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WhenScopeOwnerMatchesOrganizationWithPermission_ReturnsSuccess_Data))]
            public async Task WhenScopeOwnerMatchesOrganizationWithPermission_ReturnsSuccess(bool isAdmin, string scope)
            {
                // Arrange 
                var fakes = new Fakes();
                var user = isAdmin ? fakes.OrganizationAdmin : fakes.OrganizationCollaborator;
                var orgUser = fakes.Organization;
                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(orgUser.Username, false))
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
                    .Setup(u => u.FindByUsername(user.Username, false))
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
                    .Setup(u => u.FindByUsername(user.Username, false))
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
                    .Setup(u => u.FindByUsername(user.Username, false))
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
                    .Setup(u => u.FindByUsername(user.Username, false))
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

        public class TheProfilesAction : TestContainer
        {
            public static IEnumerable<object[]> Returns404ForMissingOrDeletedUser_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData((User)null);
                    yield return MemberDataHelper.AsData(new User("test") { IsDeleted = true });
                }
            }

            [Theory]
            [MemberData(nameof(Returns404ForMissingOrDeletedUser_Data))]
            public void Returns404ForMissingOrDeletedUser(User user)
            {
                // Arrange
                var username = "test";

                GetMock<IUserService>()
                    .Setup(x => x.FindByUsername(username, false))
                    .Returns(user);

                var controller = GetController<UsersController>();

                // Act
                var result = controller.Profiles(username);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.NotFound);
            }

            public static IEnumerable<object[]> PossibleOwnershipScenarios_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(null, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, new User("randomUser") { Key = 5535 });
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(PossibleOwnershipScenarios_Data))]
            public void ReturnsSinglePackageAsExpected(User currentUser, User owner)
            {
                // Arrange
                var username = "test";

                var package = new Package
                {
                    Version = "1.1.1",

                    PackageRegistration = new PackageRegistration
                    {
                        Id = "package",
                        Owners = new[] { owner },
                        DownloadCount = 150
                    },

                    DownloadCount = 100
                };

                GetMock<IUserService>()
                    .Setup(x => x.FindByUsername(username, false))
                    .Returns(owner);

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesByOwner(owner, false, false))
                    .Returns(new[] { package });

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = controller.Profiles(username);

                // Assert
                var model = ResultAssert.IsView<UserProfileModel>(result);
                AssertUserProfileModel(model, currentUser, owner, package);
            }

            [Theory]
            [MemberData(nameof(PossibleOwnershipScenarios_Data))]
            public void SortsPackagesByDownloadCount(User currentUser, User owner)
            {
                // Arrange
                var username = "test";

                var package1 = new Package
                {
                    Version = "1.1.1",

                    PackageRegistration = new PackageRegistration
                    {
                        Id = "package",
                        Owners = new[] { owner },
                        DownloadCount = 150
                    },

                    DownloadCount = 100
                };

                var package2 = new Package
                {
                    Version = "1.32.1",

                    PackageRegistration = new PackageRegistration
                    {
                        Id = "otherPackage",
                        Owners = new[] { owner },
                        DownloadCount = 200
                    },

                    DownloadCount = 150
                };

                GetMock<IUserService>()
                    .Setup(x => x.FindByUsername(username, false))
                    .Returns(owner);

                GetMock<IPackageService>()
                    .Setup(x => x.FindPackagesByOwner(owner, false, false))
                    .Returns(new[] { package1, package2 });

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = controller.Profiles(username);

                // Assert
                var model = ResultAssert.IsView<UserProfileModel>(result);
                AssertUserProfileModel(model, currentUser, owner, package2, package1);
            }

            private void AssertUserProfileModel(UserProfileModel model, User currentUser, User owner, params Package[] orderedPackages)
            {
                Assert.Equal(owner, model.User);
                Assert.Equal(owner.EmailAddress, model.EmailAddress);
                Assert.Equal(owner.Username, model.Username);
                Assert.Equal(orderedPackages.Count(), model.TotalPackages);
                Assert.Equal(orderedPackages.Sum(p => p.PackageRegistration.DownloadCount), model.TotalPackageDownloadCount);

                var orderedPackagesIndex = 0;
                foreach (var package in model.AllPackages)
                {
                    AssertListPackageItemViewModel(package, currentUser, orderedPackages[orderedPackagesIndex++]);
                }
            }

            private void AssertListPackageItemViewModel(
                ListPackageItemViewModel packageModel,
                User currentUser,
                Package package)
            {
                Assert.Equal(package.PackageRegistration.Id, packageModel.Id);
                Assert.Equal(package.Version, packageModel.Version);
                Assert.Equal(package.PackageRegistration.DownloadCount, packageModel.DownloadCount);

                AssertListPackageItemViewModelPermissions(packageModel, p => p.CanDisplayPrivateMetadata, currentUser, package, ActionsRequiringPermissions.DisplayPrivatePackageMetadata);
                AssertListPackageItemViewModelPermissions(packageModel, p => p.CanEdit, currentUser, package, ActionsRequiringPermissions.EditPackage);
                AssertListPackageItemViewModelPermissions(packageModel, p => p.CanUnlistOrRelist, currentUser, package, ActionsRequiringPermissions.UnlistOrRelistPackage);
                AssertListPackageItemViewModelPermissions(packageModel, p => p.CanManageOwners, currentUser, package, ActionsRequiringPermissions.ManagePackageOwnership);
                AssertListPackageItemViewModelPermissions(packageModel, p => p.CanReportAsOwner, currentUser, package, ActionsRequiringPermissions.ReportPackageAsOwner);
            }

            private void AssertListPackageItemViewModelPermissions(
                ListPackageItemViewModel packageModel,
                Func<ListPackageItemViewModel, bool> getPermissionsField,
                User currentUser,
                Package package,
                IActionRequiringEntityPermissions<Package> action)
            {
                var expectedPermissions = action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
                Assert.Equal(expectedPermissions, getPermissionsField(packageModel));
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
                var inputModel = new UserAccountViewModel
                {
                    ChangePassword = new ChangePasswordViewModel
                    {
                        DisablePasswordLogin = false,
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
                var outputModel = ResultAssert.IsView<UserAccountViewModel>(result, viewName: "Account");
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

                var inputModel = new UserAccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        DisablePasswordLogin = false,
                        OldPassword = "old",
                        NewPassword = "new",
                        VerifyPassword = "new2",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<UserAccountViewModel>(result, viewName: "Account");
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

                var inputModel = new UserAccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        DisablePasswordLogin = false,
                        OldPassword = "old",
                        NewPassword = "new",
                        VerifyPassword = "new",
                    }
                };

                // Act
                var result = await controller.ChangePassword(inputModel);

                // Assert
                var outputModel = ResultAssert.IsView<UserAccountViewModel>(result, viewName: "Account");
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
                                    It.Is<CredentialViewModel>(c => c.Type == CredentialTypes.External.MicrosoftAccount)))
                    .Verifiable();

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);
                var inputModel = new UserAccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        DisablePasswordLogin = true,
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
                var inputModel = new UserAccountViewModel()
                {
                    ChangePassword = new ChangePasswordViewModel()
                    {
                        DisablePasswordLogin = false,
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
                await controller.ChangePassword(new UserAccountViewModel());

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
                await controller.ChangePassword(new UserAccountViewModel());

                // Assert
                var errorMessages = controller
                    .ModelState["ChangePassword"]
                    .Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                Assert.Equal(errorMessages, new[] { Strings.UserIsNotYetConfirmed });
            }
        }

        public class TheChangeMultiFactorAuthenticationAction : TestContainer
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task SettingsAreUpdated_RedirectsBackWithMessage(bool enable2FA)
            {
                // Arrange
                var fakes = Get<Fakes>();
                var user = fakes.CreateUser("user1");
                user.EnableMultiFactorAuthentication = !enable2FA;

                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                var userServiceMock = GetMock<IUserService>();
                userServiceMock
                    .Setup(x => x.ChangeMultiFactorAuthentication(user, enable2FA))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                // Act
                var result = await controller.ChangeMultiFactorAuthentication(enable2FA);

                // Assert
                userServiceMock.Verify(x => x.ChangeMultiFactorAuthentication(user, enable2FA));
                Assert.NotNull(controller.TempData["Message"]);
                var identity = controller.OwinContext.Authentication.User.Identity as ClaimsIdentity;
                Assert.NotNull(identity);
                Assert.Equal(enable2FA, ClaimsExtensions.HasBooleanClaim(identity, NuGetClaims.EnabledMultiFactorAuthentication));
                ResultAssert.IsRedirectToRoute(result, new { action = "Account" });
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
                    credentialType: CredentialTypes.External.MicrosoftAccount,
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
                                    It.Is<CredentialViewModel>(c => c.Type == CredentialTypes.External.MicrosoftAccount)))
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

        public class TheLinkOrChangeCredentialAction : TestContainer
        {
            [Fact]
            public void ForNonAADLinkedAccount_RedirectsToAuthenticateExternalLogin()
            {
                // Arrange
                var fakes = Get<Fakes>();
                var passwordCred = new Credential("password.v3", "random");
                var msftCred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "bloom", "filter");
                var user = fakes.CreateUser("test", passwordCred, msftCred);
                var controller = GetController<UsersController>();
                controller.SetCurrentUser(user);

                // Act
                var result = controller.LinkOrChangeExternalCredential();

                // Assert
                Assert.IsType<RedirectResult>(result);
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
                ResultAssert.IsNotFound(result);
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
                    .Setup(stub => stub.FindByUsername(userName, false))
                    .Returns(testUser);

                // act
                var result = controller.Delete(accountName: userName);

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DeleteHappyAccount(bool withPendingIssues)
            {
                // Arrange
                string userName = "DeletedUser";
                var controller = GetController<UsersController>();
                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.IsDeleted = false;
                testUser.Key = 1;
                controller.SetCurrentUser(fakes.Admin);

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
                        OwnerEmail = testUser.EmailAddress,
                        CreatedBy = userName,
                        UserKey = testUser.Key,
                        IssueStatus = new IssueStatus() { Key = IssueStatusKeys.New, Name = "OneIssue" }
                    });
                }

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName, false))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<ISupportRequestService>()
                    .Setup(stub => stub.GetIssues(null, null, null, null))
                    .Returns(issues);

                // act
                var model = ResultAssert.IsView<DeleteUserViewModel>(controller.Delete(accountName: userName), viewName: "DeleteUserAccount");

                // Assert
                Assert.Equal(userName, model.AccountName);
                Assert.Equal(1, model.Packages.Count());
                Assert.Equal(withPendingIssues, model.HasPendingRequests);
            }
        }

        public class TheDeleteAccountRequestAction : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ShowsViewWithCorrectData(bool withPendingIssues)
            {
                // Arrange
                var controller = GetController<UsersController>();
                var fakes = Get<Fakes>();
                var testUser = fakes.User;

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
                        OwnerEmail = testUser.EmailAddress,
                        CreatedBy = testUser.Username,
                        UserKey = testUser.Key,
                        IssueStatus = new IssueStatus() { Key = IssueStatusKeys.New, Name = "OneIssue" }
                    });
                }

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(testUser.Username, false))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<ISupportRequestService>()
                   .Setup(stub => stub.GetIssues(null, null, null, null))
                   .Returns(issues);

                // act
                var result = controller.DeleteRequest() as ViewResult;
                var model = ResultAssert.IsView<DeleteUserViewModel>(result, "DeleteAccount");

                // Assert
                Assert.Equal(testUser.Username, model.AccountName);
                Assert.Equal(1, model.Packages.Count());
                Assert.Equal(true, model.HasOrphanPackages);
                Assert.Equal(withPendingIssues, model.HasPendingRequests);
            }
        }

        public class TheRequestAccountDeletionAction : TestContainer
        {
            [Fact]
            public async Task ReturnsNotFoundWithoutCurrentUser()
            {
                // Arrange
                var controller = GetController<UsersController>();

                // Act & Assert
                await ReturnsNotFound(controller);
            }

            [Fact]
            public async Task ReturnsNotFoundWithDeletedCurrentUser()
            {
                // Arrange
                var controller = GetController<UsersController>();

                var currentUser = Get<Fakes>().User;
                currentUser.IsDeleted = true;
                controller.SetCurrentUser(currentUser);

                // Act & Assert
                await ReturnsNotFound(controller);
            }

            private async Task ReturnsNotFound(UsersController controller)
            {
                var result = await controller.RequestAccountDeletion(string.Empty);
                
                ResultAssert.IsNotFound(result);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SucceedsIfSupportRequestIsAdded(bool successOnSentRequest)
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
                    .Setup(stub => stub.FindByUsername(userName, false))
                    .Returns(testUser);
                GetMock<IPackageService>()
                    .Setup(stub => stub.FindPackagesByAnyMatchingOwner(testUser, It.IsAny<bool>(), false))
                    .Returns(userPackages);
                GetMock<ISupportRequestService>()
                   .Setup(stub => stub.GetIssues(null, null, null, userName))
                   .Returns(issues);
                GetMock<ISupportRequestService>()
                  .Setup(stub => stub.TryAddDeleteSupportRequestAsync(testUser))
                  .ReturnsAsync(successOnSentRequest);

                // act
                var result = await controller.RequestAccountDeletion() as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal("DeleteRequest", (string)result.RouteValues["action"]);
                bool tempData = controller.TempData.ContainsKey("RequestFailedMessage");
                Assert.Equal(!successOnSentRequest, tempData);
                GetMock<IMessageService>()
                    .Verify(
                        stub => stub.SendAccountDeleteNotice(testUser), 
                        successOnSentRequest ? Times.Once() : Times.Never());
            }

            /// <summary>
            /// If the user does not have the email account confirmed the user record is deleted without sending a support request.
            /// </summary>
            [Fact]
            public async Task WhenUserIsUnconfirmedDeletesAccount()
            {
                // Arrange
                string userName = "DeletedUser";
                string emailAddress = $"{userName}@coldmail.com";

                var controller = GetController<UsersController>();

                var fakes = Get<Fakes>();
                var testUser = fakes.CreateUser(userName);
                testUser.UnconfirmedEmailAddress = emailAddress;
                controller.SetCurrentUser(testUser);

                GetMock<IUserService>()
                    .Setup(stub => stub.FindByUsername(userName, false))
                    .Returns(testUser);

                GetMock<IDeleteAccountService>()
                    .Setup(stub => stub.DeleteAccountAsync(testUser, It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<AccountDeletionOrphanPackagePolicy>(), It.IsAny<string>()))
                    .Returns(value: Task.FromResult(new DeleteUserAccountStatus()
                    {
                        AccountName = userName,
                        Description = "Delete user",
                        Success = true
                    }));

                // act
                var result = await controller.RequestAccountDeletion() as NuGetGallery.SafeRedirectResult;

                // Assert
                Assert.NotNull(result);
            }
        }

        public class TheTransformToOrganizationActionBase : TestContainer
        {
            protected UsersController CreateController(string accountToTransform, string canTransformErrorReason = "")
            {
                var configurationService = GetConfigurationService();

                var controller = GetController<UsersController>();
                var currentUser = new User(accountToTransform) { EmailAddress = $"{accountToTransform}@example.com" };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername("OrgAdmin", false))
                    .Returns(new User("OrgAdmin")
                    {
                        EmailAddress = "orgadmin@example.com"
                    });

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(s => s.RequestTransformToOrganizationAccount(It.IsAny<User>(), It.IsAny<User>()))
                    .Callback<User, User>((acct, admin) =>
                    {
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
                var result = controller.TransformToOrganization() as ViewResult;

                // Assert
                Assert.NotNull(result);

                var model = result.Model as TransformAccountFailedViewModel;
                Assert.Equal("error", model.ErrorMessage);
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
                var result = await controller.TransformToOrganization(new TransformAccountViewModel()
                {
                    AdminUsername = "OrgAdmin"
                }) as ViewResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(1, controller.ModelState[string.Empty].Errors.Count);
                Assert.Equal("error", controller.ModelState[string.Empty].Errors.First().ErrorMessage);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequest(
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never());

                GetMock<IMessageService>()
                    .Verify(
                        m => m.SendOrganizationTransformInitiatedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<string>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformInitiated(It.IsAny<User>()),
                        Times.Never());
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
                Assert.Equal(1, controller.ModelState[string.Empty].Errors.Count);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture,
                        Strings.TransformAccount_AdminAccountDoesNotExist, "AdminThatDoesNotExist"),
                    controller.ModelState[string.Empty].Errors.First().ErrorMessage);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequest(
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never());

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformInitiatedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>(),
                            It.IsAny<string>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformInitiated(It.IsAny<User>()),
                        Times.Never());
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

                GetMock<IMessageService>()
                    .Verify(m => m.SendOrganizationTransformRequest(
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()));

                GetMock<IMessageService>()
                    .Verify(m => m.SendOrganizationTransformInitiatedNotice(
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<string>()));

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformInitiated(It.IsAny<User>()));
            }
        }

        public class TheConfirmTransformToOrganizationAction : TestContainer
        {
            [Fact]
            public async Task WhenAccountToTransformIsNotFound_ShowsError()
            {
                // Arrange
                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.ConfirmTransformToOrganization("account", "token") as ViewResult;

                // Assert
                Assert.NotNull(result);

                var model = result.Model as TransformAccountFailedViewModel;
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture, Strings.TransformAccount_OrganizationAccountDoesNotExist, "account"),
                    model.ErrorMessage);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestAcceptedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCompleted(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenCanTransformReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, canTransformErrorReason: "error");

                // Act
                var result = await controller.ConfirmTransformToOrganization(accountToTransform, "token") as ViewResult;

                // Assert
                Assert.NotNull(result);

                var model = result.Model as TransformAccountFailedViewModel;
                Assert.Equal(
                    "error",
                    model.ErrorMessage);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestAcceptedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCompleted(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenUserServiceReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, success: false);

                // Act
                var result = await controller.ConfirmTransformToOrganization(accountToTransform, "token") as ViewResult;

                // Assert
                Assert.NotNull(result);

                var model = result.Model as TransformAccountFailedViewModel;
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture,
                        Strings.TransformAccount_Failed, "account"),
                    model.ErrorMessage);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestAcceptedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCompleted(It.IsAny<Organization>()),
                        Times.Never());
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

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestAcceptedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()));

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCompleted(It.IsAny<User>()));
            }

            private UsersController CreateController(string accountToTransform, string canTransformErrorReason = "", bool success = true)
            {
                // Arrange
                var configurationService = GetConfigurationService();

                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(accountToTransform, false))
                    .Returns(new User(accountToTransform)
                    {
                        EmailAddress = $"{accountToTransform}@example.com"
                    });

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(u => u.CanTransformUserToOrganization(It.IsAny<User>(), It.IsAny<User>(), out canTransformErrorReason))
                    .Returns(string.IsNullOrEmpty(canTransformErrorReason));

                GetMock<IUserService>()
                    .Setup(s => s.TransformUserToOrganization(It.IsAny<User>(), It.IsAny<User>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(success));

                return controller;
            }
        }

        public class TheRejectTransformToOrganizationAction : TestContainer
        {
            [Fact]
            public async Task WhenAccountToTransformIsNotFound_ShowsError()
            {
                // Arrange
                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.RejectTransformToOrganization("account", "token") as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture, Strings.TransformAccount_OrganizationAccountDoesNotExist, "account"),
                    controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestRejectedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformDeclined(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenUserServiceReturnsFalse_ShowsError()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, success: false);

                // Act
                var result = await controller.RejectTransformToOrganization(accountToTransform, "token") as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(Strings.TransformAccount_FailedMissingRequestToCancel, controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestRejectedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformDeclined(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenUserServiceReturnsSuccess_Redirects()
            {
                // Arrange
                var accountToTransform = "account";
                var controller = CreateController(accountToTransform, success: true);

                // Act
                var result = await controller.RejectTransformToOrganization(accountToTransform, "token");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture, Strings.TransformAccount_Rejected, accountToTransform),
                    controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestRejectedNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()));

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformDeclined(It.IsAny<User>()));
            }

            private UsersController CreateController(string accountToTransform, bool success = true)
            {
                // Arrange
                var configurationService = GetConfigurationService();

                var controller = GetController<UsersController>();
                var currentUser = new User("OrgAdmin") { EmailAddress = "orgadmin@example.com" };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(u => u.FindByUsername(accountToTransform, false))
                    .Returns(new User(accountToTransform)
                    {
                        EmailAddress = $"{accountToTransform}@example.com"
                    });

                GetMock<IUserService>()
                    .Setup(s => s.RejectTransformUserToOrganizationRequest(It.IsAny<User>(), It.IsAny<User>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(success));

                return controller;
            }
        }

        public class TheCancelTransformToOrganizationAction : TestContainer
        {
            [Fact]
            public async Task WhenUserServiceReturnsFalse_ShowsError()
            {
                // Arrange
                var controller = CreateController(success: false);

                // Act
                var result = await controller.CancelTransformToOrganization("token") as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    Strings.TransformAccount_FailedMissingRequestToCancel,
                    controller.TempData["ErrorMessage"]);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestCancelledNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()),
                        Times.Never());

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCancelled(It.IsAny<Organization>()),
                        Times.Never());
            }

            [Fact]
            public async Task WhenUserServiceReturnsSuccess_Redirects()
            {
                // Arrange
                var controller = CreateController(success: true);

                // Act
                var result = await controller.CancelTransformToOrganization("token") as RedirectToRouteResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(
                    String.Format(CultureInfo.CurrentCulture, Strings.TransformAccount_Cancelled),
                    controller.TempData["Message"]);

                GetMock<IMessageService>()
                    .Verify(m =>
                        m.SendOrganizationTransformRequestCancelledNotice(
                            It.IsAny<User>(),
                            It.IsAny<User>()));

                GetMock<ITelemetryService>()
                    .Verify(
                        t => t.TrackOrganizationTransformCancelled(It.IsAny<User>()));
            }

            private UsersController CreateController(bool success = true)
            {
                // Arrange
                var configurationService = GetConfigurationService();

                var controller = GetController<UsersController>();
                var admin = new User("OrgToBe") { EmailAddress = "org@example.com" };
                var currentUser = new User("OrgToBe")
                {
                    EmailAddress = "orgToBe@example.com",
                    OrganizationMigrationRequest = new OrganizationMigrationRequest
                    {
                        AdminUser = admin,
                        ConfirmationToken = "token"
                    }
                };
                controller.SetCurrentUser(currentUser);

                GetMock<IUserService>()
                    .Setup(s => s.CancelTransformUserToOrganizationRequest(It.IsAny<User>(), It.IsAny<string>()))
                    .Returns(Task.FromResult(success));

                return controller;
            }
        }

        public class TheGetCertificateAction : TestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly UsersController _controller;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheGetCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _controller = GetController<UsersController>();
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _certificate = new Certificate()
                {
                    Key = 2,
                    Sha1Thumbprint = "b",
                    Thumbprint = "c"
                };
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void GetCertificate_WhenThumbprintIsInvalid_ReturnsBadRequest(string thumbprint)
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.GetCertificate(accountName: null, thumbprint: thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.GetCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificate_WhenCurrentUserHasNoCertificates_ReturnsOK()
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _user)))
                    .Returns(Enumerable.Empty<Certificate>());
                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.GetCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Empty((IEnumerable<ListCertificateItemViewModel>)response.Data);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void GetCertificate_WhenCurrentUserHasCertificate_ReturnsOK()
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _user)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.GetCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.True(viewModel.CanDelete);
                Assert.Equal($"/account/certificates/{_certificate.Thumbprint}", viewModel.DeleteUrl);
                Assert.Equal(_certificate.Sha1Thumbprint, viewModel.Sha1Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }
        }

        public class TheGetCertificatesAction : TestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly UsersController _controller;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheGetCertificatesAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _controller = GetController<UsersController>();
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _certificate = new Certificate()
                {
                    Key = 2,
                    Sha1Thumbprint = "b",
                    Thumbprint = "c"
                };
            }

            [Fact]
            public void GetCertificates_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.GetCertificates(accountName: null);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void GetCertificates_WhenCurrentUserHasNoCertificates_ReturnsOK()
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _user)))
                    .Returns(Enumerable.Empty<Certificate>());
                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.GetCertificates(accountName: null);

                Assert.NotNull(response);
                Assert.Empty((IEnumerable<ListCertificateItemViewModel>)response.Data);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void GetCertificates_WhenCurrentUserHasCertificate_ReturnsOK()
            {
                _certificateService.Setup(x => x.GetCertificates(It.Is<User>(u => u == _user)))
                    .Returns(new[] { _certificate });
                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.GetCertificates(accountName: null);

                Assert.NotNull(response);
                Assert.NotEmpty((IEnumerable<ListCertificateItemViewModel>)response.Data);

                var viewModel = ((IEnumerable<ListCertificateItemViewModel>)response.Data).Single();

                Assert.True(viewModel.CanDelete);
                Assert.Equal($"/account/certificates/{_certificate.Thumbprint}", viewModel.DeleteUrl);
                Assert.Equal(_certificate.Sha1Thumbprint, viewModel.Sha1Thumbprint);
                Assert.Equal(JsonRequestBehavior.AllowGet, response.JsonRequestBehavior);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }
        }

        public class TheAddCertificateAction : TestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly Mock<ISecurityPolicyService> _securityPolicyService;
            private readonly Mock<IPackageService> _packageService;
            private readonly UsersController _controller;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheAddCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _securityPolicyService = GetMock<ISecurityPolicyService>();
                _packageService = GetMock<IPackageService>();
                _controller = GetController<UsersController>();
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _certificate = new Certificate()
                {
                    Key = 2,
                    Sha1Thumbprint = "b",
                    Thumbprint = "c"
                };
            }

            [Fact]
            public void AddCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var uploadFile = new StubHttpPostedFile(contentLength: 0, fileName: "a.cer", inputStream: Stream.Null);
                var response = _controller.AddCertificate(accountName: null, uploadFile: uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void AddCertificate_WhenUploadFileIsNull_ReturnsBadRequest()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.AddCertificate(accountName: null, uploadFile: null);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Fact]
            public void AddCertificate_WhenCurrentUserIsNotMultiFactorAuthenticated_ReturnsForbidden()
            {
                var uploadFile = GetUploadFile();

                _controller.SetCurrentUser(_user);

                var response = _controller.AddCertificate(accountName: null, uploadFile: uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void AddCertificate_WhenUploadFileIsValid_ReturnsCreated()
            {
                var uploadFile = GetUploadFile();

                _certificateService.Setup(x => x.AddCertificateAsync(
                        It.Is<HttpPostedFileBase>(file => ReferenceEquals(file, uploadFile))))
                    .ReturnsAsync(_certificate);
                _certificateService.Setup(x => x.ActivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _user)))
                    .Returns(Task.CompletedTask);

                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.AddCertificate(accountName: null, uploadFile: uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Created, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
            }

            [Fact]
            public void AddCertificate_WhenUserIsSubscribedToAutomaticallyOverwriteRequiredSignerPolicy_ReturnsCreated()
            {
                var uploadFile = GetUploadFile();

                _certificateService.Setup(x => x.AddCertificateAsync(
                        It.Is<HttpPostedFileBase>(file => ReferenceEquals(file, uploadFile))))
                    .ReturnsAsync(_certificate);
                _certificateService.Setup(x => x.ActivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _user)))
                    .Returns(Task.CompletedTask);
                _certificateService.Setup(x => x.GetCertificates(
                        It.Is<User>(user => user == _user)))
                    .Returns(new[] { _certificate });
                _securityPolicyService.Setup(x => x.IsSubscribed(
                        It.Is<User>(user => user == _user),
                        It.Is<string>(policyName => policyName == AutomaticallyOverwriteRequiredSignerPolicy.PolicyName)))
                    .Returns(true);
                _packageService.Setup(x => x.SetRequiredSignerAsync(It.Is<User>(user => user == _user)))
                    .Returns(Task.CompletedTask);

                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.AddCertificate(accountName: null, uploadFile: uploadFile);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Created, _controller.Response.StatusCode);

                _certificateService.VerifyAll();
                _securityPolicyService.VerifyAll();
                _packageService.Verify(x => x.SetRequiredSignerAsync(_user), Times.Once);
            }

            private static StubHttpPostedFile GetUploadFile()
            {
                var bytes = Encoding.UTF8.GetBytes("certificate");
                var stream = new MemoryStream(bytes);

                return new StubHttpPostedFile((int)stream.Length, "certificate.cer", stream);
            }
        }

        public class TheDeleteCertificateAction : TestContainer
        {
            private readonly Mock<ICertificateService> _certificateService;
            private readonly UsersController _controller;
            private readonly User _user;
            private readonly Certificate _certificate;

            public TheDeleteCertificateAction()
            {
                _certificateService = GetMock<ICertificateService>();
                _controller = GetController<UsersController>();
                _user = new User()
                {
                    Key = 1,
                    Username = "a"
                };
                _certificate = new Certificate()
                {
                    Key = 2,
                    Sha1Thumbprint = "b",
                    Thumbprint = "c"
                };
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void DeleteCertificate_WhenThumbprintIsInvalid_ReturnsBadRequest(string thumbprint)
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.DeleteCertificate(accountName: null, thumbprint: thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.BadRequest, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenCurrentUserIsNull_ReturnsUnauthorized()
            {
                var response = _controller.DeleteCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Unauthorized, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WhenCurrentUserIsNotMultiFactorAuthenticated_ReturnsForbidden()
            {
                _controller.SetCurrentUser(_user);

                var response = _controller.DeleteCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.Forbidden, _controller.Response.StatusCode);
            }

            [Fact]
            public void DeleteCertificate_WithValidThumbprint_ReturnsOK()
            {
                _certificateService.Setup(x => x.DeactivateCertificateAsync(
                        It.Is<string>(thumbprint => thumbprint == _certificate.Thumbprint),
                        It.Is<User>(user => user == _user)))
                    .Returns(Task.CompletedTask);
                _controller.SetCurrentUser(_user);
                _controller.OwinContext.AddClaim(NuGetClaims.WasMultiFactorAuthenticated);

                var response = _controller.DeleteCertificate(accountName: null, thumbprint: _certificate.Thumbprint);

                Assert.NotNull(response);
                Assert.Equal((int)HttpStatusCode.OK, _controller.Response.StatusCode);
            }
        }
    }
}