// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class UserServiceFacts
    {
        public class TheAddMemberAsyncMethod
        {
            public Fakes Fakes { get; }

            public TestableUserService UserService { get; }

            public TheAddMemberAsyncMethod()
            {
                Fakes = new Fakes();
                UserService = new TestableUserService();

                UserService.MockUserRepository.Setup(r => r.GetAll())
                    .Returns(new[] {
                        Fakes.User,
                        Fakes.Organization
                    }.AsQueryable());
            }

            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await UserService.AddMemberAsync(null, "member", false);
                });
            }

            [Fact]
            public async Task WhenMemberExists_ThrowsEntityException()
            {
                // Act & Assert
                await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.OrganizationCollaborator.Username, false);
                });

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenUserNotFound_ThrowsEntityException()
            {
                // Act & Assert
                await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, "notAUser", false);
                });

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenSecurityPolicyEvalutionFailure_ThrowsEntityException()
            {
                // Arrange
                UserService.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, Fakes.Organization, Fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("error")));

                // Act & Assert
                await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await UserService.AddMemberAsync(Fakes.Organization, Fakes.User.Username, isAdmin: true);
                });

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenSecurityPolicyEvalutionSuccess_CreatesMembership(bool isAdmin)
            {
                // Arrange
                UserService.MockSecurityPolicyService
                    .Setup(s => s.EvaluateOrganizationPoliciesAsync(SecurityPolicyAction.JoinOrganization, Fakes.Organization, Fakes.User))
                    .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));

                // Act
                var result = await UserService.AddMemberAsync(
                    Fakes.Organization,
                    Fakes.User.Username,
                    isAdmin);

                // Assert
                Assert.Equal(isAdmin, result.IsAdmin);
                Assert.Equal(Fakes.User, result.Member);

                UserService.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheDeleteMemberAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.DeleteMemberAsync(null, "member");
                });
            }
            
            [Fact]
            public async Task WhenNoMatch_ThrowsEntityException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.DeleteMemberAsync(new Organization(), "member");
                });
            }

            [Fact]
            public async Task WhenLastAdmin_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);

                Assert.Equal(
                    Strings.DeleteMember_CannotRemoveLastAdmin,
                    exception.Message);
            }

            [Fact]
            public async Task WhenNotLastAdmin_DeletesMembership()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                foreach (var m in fakes.Organization.Members)
                {
                    m.IsAdmin = true;
                }

                // Act
                await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username);
                
                // Assert
                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task WhenCollaborator_DeletesMembership()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act
                await service.DeleteMemberAsync(fakes.Organization, fakes.OrganizationCollaborator.Username);

                // Assert
                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheUpdateMemberAsyncMethod
        {
            [Fact]
            public async Task WhenOrganizationIsNull_ThrowsException()
            {
                // Arrange
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                {
                    await service.UpdateMemberAsync(null, "member", false);
                });
            }

            [Fact]
            public async Task WhenMemberNotFound_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.UpdateMemberAsync(fakes.Organization, fakes.User.Username, false);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task WhenRemovingLastAdmin_ThrowsEntityException()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<EntityException>(async () =>
                {
                    await service.UpdateMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username, false);
                });

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Never);
                Assert.Equal(
                    Strings.UpdateMember_CannotRemoveLastAdmin,
                    exception.Message);
            }

            [Fact]
            public async Task WhenNotRemovingLastAdmin_ReturnsSuccess()
            {
                // Arrange
                var fakes = new Fakes();
                var service = new TestableUserService();
                foreach (var m in fakes.Organization.Members)
                {
                    m.IsAdmin = true;
                }

                // Act
                var result = await service.UpdateMemberAsync(fakes.Organization, fakes.OrganizationAdmin.Username, false);

                // Assert
                Assert.Equal(false, result.IsAdmin);
                Assert.Equal(fakes.OrganizationAdmin, result.Member);

                service.MockEntitiesContext.Verify(c => c.SaveChangesAsync(), Times.Once);
            }
        }

        public class TheConfirmEmailAddressMethod
        {
            [Fact]
            public async Task WithTokenThatDoesNotMatchUserReturnsFalse()
            {
                var user = new User { Username = "username", EmailConfirmationToken = "token" };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "not-token");

                Assert.False(confirmed);
            }

            [Fact]
            public async Task ThrowsForDuplicateConfirmedEmailAddresses()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "old@example.org", UnconfirmedEmailAddress = "new@example.org", EmailConfirmationToken = "token" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var ex = await AssertEx.Throws<EntityException>(() => service.ConfirmEmailAddress(user, "token"));
                Assert.Equal(String.Format(Strings.EmailAddressBeingUsed, "new@example.org"), ex.Message);
            }

            [Fact]
            public async Task WithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public async Task ForUserWithConfirmedEmailWithTokenThatDoesMatchUserConfirmsUserAndReturnsTrue()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    EmailAddress = "existing@example.com",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(confirmed);
                Assert.True(user.Confirmed);
                Assert.Equal("new@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            [Fact]
            public async Task WithNullUserThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                await AssertEx.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(null, "token"));
            }

            [Fact]
            public async Task WithEmptyTokenThrowsArgumentNullException()
            {
                var service = new TestableUserService();

                await AssertEx.Throws<ArgumentNullException>(() => service.ConfirmEmailAddress(new User(), ""));
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                var user = new User
                {
                    Username = "username",
                    EmailConfirmationToken = "secret",
                    EmailAddress = "existing@example.com",
                    UnconfirmedEmailAddress = "new@example.com"
                };
                var service = new TestableUserService();

                var confirmed = await service.ConfirmEmailAddress(user, "secret");

                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ConfirmEmail &&
                    ar.AffectedEmailAddress == "new@example.com"));
            }
        }

        public class TheFindByEmailAddressMethod
        {
            [Fact]
            public void ReturnsNullIfMultipleMatchesExist()
            {
                var user = new User { Username = "User1", Key = 1, EmailAddress = "new@example.org" };
                var conflictingUser = new User { Username = "User2", Key = 2, EmailAddress = "new@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user, conflictingUser }
                };

                var result = service.FindByEmailAddress("new@example.org");
                Assert.Null(result);
            }
        }

        public class TheChangeEmailMethod
        {
            [Fact]
            public async Task SetsUnconfirmedEmailWhenEmailIsChanged()
            {
                var user = new User { Username = "Bob", EmailAddress = "old@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.ChangeEmailAddress(user, "new@example.org");

                Assert.Equal("old@example.org", user.EmailAddress);
                Assert.Equal("new@example.org", user.UnconfirmedEmailAddress);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It has to change the pending confirmation token whenever address changes because otherwise you can do
            /// 1. change address, get confirmation email
            /// 2. change email address again to something you don't own
            /// 3. hit confirm and you confirmed an email address you don't own
            /// </summary>
            [Fact]
            public async Task ModifiesConfirmationTokenWhenEmailAddressChanged()
            {
                var user = new User { EmailAddress = "old@example.com", EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "new@example.com");
                Assert.NotNull(user.EmailConfirmationToken);
                Assert.NotEmpty(user.EmailConfirmationToken);
                Assert.NotEqual("pending-token", user.EmailConfirmationToken);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            /// <summary>
            /// It would be annoying if you start seeing pending email changes as a result of NOT changing your email address.
            /// </summary>
            [Fact]
            public async Task DoesNotModifyAnythingWhenConfirmedEmailAddressNotChanged()
            {
                var user = new User { EmailAddress = "old@example.com", UnconfirmedEmailAddress = null, EmailConfirmationToken = null };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "old@example.com");
                Assert.True(user.Confirmed);
                Assert.Equal("old@example.com", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                Assert.Null(user.EmailConfirmationToken);
            }

            /// <summary>
            /// Because it's bad if your confirmation email no longer works because you did a no-op email address change.
            /// </summary>
            [Theory]
            [InlineData("something@else.com")]
            [InlineData(null)]
            public async Task DoesNotModifyConfirmationTokenWhenUnconfirmedEmailAddressNotChanged(string confirmedEmailAddress)
            {
                var user = new User {
                    EmailAddress = confirmedEmailAddress,
                    UnconfirmedEmailAddress = "old@example.com",
                    EmailConfirmationToken = "pending-token" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user },
                };

                await service.ChangeEmailAddress(user, "old@example.com");
                Assert.Equal("pending-token", user.EmailConfirmationToken);
            }

            [Fact]
            public async Task DoesNotLetYouUseSomeoneElsesConfirmedEmailAddress()
            {
                var user = new User { EmailAddress = "old@example.com", Key = 1 };
                var conflictingUser = new User { EmailAddress = "new@example.com", Key = 2 };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new User[] { user, conflictingUser },
                };

                var e = await AssertEx.Throws<EntityException>(() => service.ChangeEmailAddress(user, "new@example.com"));
                Assert.Equal(string.Format(Strings.EmailAddressBeingUsed, "new@example.com"), e.Message);
                Assert.Equal("old@example.com", user.EmailAddress);
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var user = new User { Username = "Bob", EmailAddress = "old@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                // Act
                await service.ChangeEmailAddress(user, "new@example.org");

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.ChangeEmail &&
                    ar.AffectedEmailAddress == "new@example.org" &&
                    ar.EmailAddress == "old@example.org"));
            }
        }

        public class TheCancelChangeEmailAddressMethod
        {
            [Fact]
            public async Task ClearsUnconfirmedEmail()
            {
                var user = new User { Username = "Bob", UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.CancelChangeEmailAddress(user);

                Assert.Equal("confirmedEmail@example.org", user.EmailAddress);
                Assert.Null(user.UnconfirmedEmailAddress);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            [Fact]
            public async Task ClearsEmailConfirmationToken()
            {
                var user = new User { Username = "Bob", EmailConfirmationToken = Guid.NewGuid().ToString() ,UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                await service.CancelChangeEmailAddress(user);

                Assert.Equal("confirmedEmail@example.org", user.EmailAddress);
                Assert.Null(user.EmailConfirmationToken);
                service.FakeEntitiesContext.VerifyCommitChanges();
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var user = new User { Username = "Bob", EmailConfirmationToken = Guid.NewGuid().ToString(), UnconfirmedEmailAddress = "unconfirmedEmail@example.org", EmailAddress = "confirmedEmail@example.org" };
                var service = new TestableUserServiceWithDBFaking
                {
                    Users = new[] { user }
                };

                // Act
                await service.CancelChangeEmailAddress(user);

                // Assert
                Assert.True(service.Auditing.WroteRecord<UserAuditRecord>(ar =>
                    ar.Action == AuditedUserAction.CancelChangeEmail &&
                    ar.AffectedEmailAddress == "unconfirmedEmail@example.org" &&
                    ar.EmailAddress == "confirmedEmail@example.org"));
            }
        }


        public class TheUpdateProfileMethod
        {
            [Fact]
            public async Task SavesEmailSettings()
            {
                var user = new User { EmailAddress = "old@example.org", EmailAllowed = true, NotifyPackagePushed = true};
                var service = new TestableUserService();
                service.MockUserRepository
                       .Setup(r => r.GetAll())
                       .Returns(new[] { user }.AsQueryable());
                
                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);
                
                // Enable contact notifications
                await service.ChangeEmailSubscriptionAsync(user, true, false);
                Assert.Equal(true, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Enable package pushed notifications
                await service.ChangeEmailSubscriptionAsync(user, false, true);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(true, user.NotifyPackagePushed);

                // Disable notifications
                await service.ChangeEmailSubscriptionAsync(user, false, false);
                Assert.Equal(false, user.EmailAllowed);
                Assert.Equal(false, user.NotifyPackagePushed);

                // Enable all notifications
                await service.ChangeEmailSubscriptionAsync(user, true, true);
                Assert.Equal(true, user.EmailAllowed);
                Assert.Equal(true, user.NotifyPackagePushed);

                service.MockUserRepository
                       .Verify(r => r.CommitChangesAsync());
            }

            [Fact]
            public async Task ThrowsArgumentExceptionForNullUser()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(async () => await service.ChangeEmailSubscriptionAsync(null, emailAllowed: true, notifyPackagePushed: true), "user");
            }
        }

        public class TheCanTransformToOrganizationMethod
        {
            [Fact]
            public void WhenAccountIsNotConfirmed_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var unconfirmedUser = new User() { UnconfirmedEmailAddress = "unconfirmed@example.com" };

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(unconfirmedUser, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountNotConfirmed, unconfirmedUser.Username));
            }

            [Fact]
            public void WhenAccountIsOrganization_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(fakes.Organization, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountIsAnOrganization, fakes.Organization.Username));
            }

            [Fact]
            public void WhenAccountHasMemberships_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(fakes.OrganizationCollaborator, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AccountHasMemberships, fakes.OrganizationCollaborator.Username));
            }

            [Fact]
            public void WhenAccountIsInWhitelist_ReturnsTrue()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, out errorReason);

                // Assert
                Assert.True(result);
            }
        }

        public class TheCanTransformToOrganizationWithAdminMethod
        {
            [Fact]
            public void WhenAdminMatchesAccountToTransform_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, user, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminMustBeDifferentAccount, user.Username));
            }

            [Fact]
            public void WhenAdminIsNotConfirmed_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var unconfirmedUser = new User() { UnconfirmedEmailAddress = "unconfirmed@example.com" };
                var user = fakes.User;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, unconfirmedUser, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountNotConfirmed, unconfirmedUser.Username));
            }

            [Fact]
            public void WhenAdminIsOrganization_ReturnsFalse()
            {
                // Arrange
                var service = new TestableUserService();
                var fakes = new Fakes();
                var user = fakes.User;
                var organization = fakes.Organization;

                // Act
                string errorReason;
                var result = service.CanTransformUserToOrganization(user, organization, out errorReason);

                // Assert
                Assert.False(result);
                Assert.Equal(errorReason, String.Format(CultureInfo.CurrentCulture,
                    Strings.TransformAccount_AdminAccountIsOrganization, organization.Username));
            }
        }

        public class TheRequestTransformToOrganizationAccountMethod
        {
            [Fact]
            public async Task WhenAccountIsNull_ThrowsNullRefException()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(
                    async () => await service.RequestTransformToOrganizationAccount(accountToTransform: null, adminUser: new User("admin")),
                    "accountToTransform");
            }

            [Fact]
            public async Task WhenAdminUserIsNull_ThrowsNullRefException()
            {
                var service = new TestableUserService();

                await ContractAssert.ThrowsArgNullAsync(
                    async () => await service.RequestTransformToOrganizationAccount(accountToTransform: new User("account"), adminUser: null),
                    "adminUser");
            }

            [Fact]
            public Task WhenExistingRequest_Overwrites()
            {
                return VerifyCreatesRequest(testOverwrite: true);
            }

            [Fact]
            public Task WhenNoExistingRequest_CreatesNew()
            {
                return VerifyCreatesRequest(testOverwrite: false);
            }

            private async Task VerifyCreatesRequest(bool testOverwrite)
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                var admin = CreateAdminUser();

                service.MockUserRepository.Setup(r => r.CommitChangesAsync()).Returns(Task.CompletedTask).Verifiable();
                
                DateTime? requestDate = null;
                string requestToken = null;
                for (int i = 0; i < (testOverwrite ? 2 : 1); i++)
                {
                    // Act
                    await service.RequestTransformToOrganizationAccount(account, admin);

                    if (testOverwrite)
                    {
                        if (requestDate != null)
                        {
                            Assert.True(requestDate < account.OrganizationMigrationRequest.RequestDate);
                            Assert.NotEqual(requestToken, account.OrganizationMigrationRequest.ConfirmationToken);
                        }

                        requestDate = account.OrganizationMigrationRequest.RequestDate;
                        requestToken = account.OrganizationMigrationRequest.ConfirmationToken;
                        await Task.Delay(500); // ensure next requestDate is in future
                    }

                    // Assert
                    service.MockUserRepository.Verify(r => r.CommitChangesAsync(), Times.Once);
                    service.MockUserRepository.ResetCalls();

                    Assert.NotNull(account.OrganizationMigrationRequest);
                    Assert.Equal(account, account.OrganizationMigrationRequest.NewOrganization);
                    Assert.Equal(admin, account.OrganizationMigrationRequest.AdminUser);
                    Assert.False(String.IsNullOrEmpty(account.OrganizationMigrationRequest.ConfirmationToken));

                    if (testOverwrite)
                    {
                        admin = CreateAdminUser();
                    }
                }
            }

            private User CreateAdminUser()
            {
                var admin = new User($"Admin-{DateTime.UtcNow.Ticks}");
                admin.Credentials.Add(
                    new CredentialBuilder().CreateExternalCredential(
                        issuer: "MicrosoftAccount",
                        value: "abc123",
                        identity: "Admin",
                        tenantId: "zyx987"));
                return admin;
            }
        }

        public class TheTransformToOrganizationAccountMethod
        {
            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            public async Task WhenSqlResultIsZeroOrLess_ReturnsFalse(int affectedRecords)
            {
                Assert.False(await InvokeTransformUserToOrganization(affectedRecords));
            }

            [Fact]
            public async Task WhenAdminHasNoTenant_ReturnsFalse()
            {
                Assert.False(await InvokeTransformUserToOrganization(3, new User("adminWithNoTenant")));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(3)]
            public async Task WhenSqlResultIsPositive_ReturnsTrue(int affectedRecords)
            {
                Assert.True(await InvokeTransformUserToOrganization(affectedRecords));
            }

            private Task<bool> InvokeTransformUserToOrganization(int affectedRecords, User admin = null)
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                admin = admin ?? new User("Admin")
                {
                    Credentials = new Credential[] {
                        new CredentialBuilder().CreateExternalCredential(
                            issuer: "AzureActiveDirectory",
                            value: "abc123",
                            identity: "Admin",
                            tenantId: "zyx987")
                    }
                };

                service.MockDatabase
                    .Setup(db => db.ExecuteSqlResourceAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .Returns(Task.FromResult(affectedRecords));

                service.MockSecurityPolicyService
                    .Setup(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), true))
                    .Returns(Task.FromResult(true));

                // Act
                return service.TransformUserToOrganization(account, admin, "token");
            }
        }
        
        public class TheTransferApiKeysScopedToUserMethod
        {
            public static IEnumerable<object[]> TransfersApiKeysAsExpected_Data
            {
                get
                {
                    foreach (var hasExternalCredential in new[] { false, true })
                    {
                        foreach (var hasPasswordCredential in new[] { false, true })
                        {
                            foreach (var hasUnscopedApiKeyCredential in new[] { false, true })
                            {
                                foreach (var hasApiKeyScopedToUserCredential in new[] { false, true })
                                {
                                    foreach (var hasApiKeyScopedToDifferentUser in new[] { false, true })
                                    {
                                        yield return MemberDataHelper.AsData(
                                            hasExternalCredential, 
                                            hasPasswordCredential, 
                                            hasUnscopedApiKeyCredential, 
                                            hasApiKeyScopedToUserCredential, 
                                            hasApiKeyScopedToDifferentUser);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(TransfersApiKeysAsExpected_Data))]
            public async Task TransfersApiKeysAsExpected(
                bool hasExternalCredential, 
                bool hasPasswordCredential,
                bool hasUnscopedApiKeyCredential, 
                bool hasApiKeyScopedToUserCredential, 
                bool hasApiKeyScopedToDifferentUser)
            {
                // Arrange
                var originalOwner = new User("originalOwner") { Key = 11111 };
                var randomUser = new User("randomUser") { Key = 57576768 };
                var newOwner = new User("newOwner") { Key = 69785, Credentials = new List<Credential>() };

                var credentials = new List<Credential>();

                var externalCredential = TestCredentialHelper.CreateExternalCredential("cred", null);
                AddFieldsToCredential(externalCredential, "externalCredential", "value1", originalOwner, expiration: null);

                var passwordCredential = TestCredentialHelper.CreateSha1Password("password");
                AddFieldsToCredential(passwordCredential, "passwordCredential", "value2", originalOwner, expiration: null);

                var unscopedApiKeyCredential = TestCredentialHelper.CreateV4ApiKey(new TimeSpan(5, 5, 5, 5), out var key1);
                AddFieldsToCredential(unscopedApiKeyCredential, "unscopedApiKey", "value3", originalOwner, expiration: new DateTime(2018, 3, 9));

                var scopedToUserApiKeyCredential = TestCredentialHelper.CreateV4ApiKey(new TimeSpan(5, 5, 5, 5), out var key2)
                            .WithScopes(new[] { new Scope { Owner = originalOwner, OwnerKey = originalOwner.Key } });
                AddFieldsToCredential(scopedToUserApiKeyCredential, "scopedToUserApiKey", "value4", originalOwner, expiration: new DateTime(2018, 3, 10));

                var scopedToDifferentUserApiKeyCredential = TestCredentialHelper.CreateV4ApiKey(new TimeSpan(5, 5, 5, 5), out var key3)
                            .WithScopes(new[] { new Scope { Owner = randomUser, OwnerKey = randomUser.Key } });
                AddFieldsToCredential(scopedToDifferentUserApiKeyCredential, "scopedToDifferentUserApiKey", "value5", originalOwner, expiration: new DateTime(2018, 3, 11));

                if (hasExternalCredential)
                {
                    credentials.Add(externalCredential);
                }

                if (hasPasswordCredential)
                {
                    credentials.Add(passwordCredential);
                }

                if (hasUnscopedApiKeyCredential)
                {
                    credentials.Add(unscopedApiKeyCredential);
                }

                if (hasApiKeyScopedToUserCredential)
                {
                    credentials.Add(scopedToUserApiKeyCredential);
                }
                
                if (hasApiKeyScopedToDifferentUser)
                {
                    credentials.Add(scopedToDifferentUserApiKeyCredential);
                }

                originalOwner.Credentials = credentials;
                var originalCredentialCount = credentials.Count();

                var service = new TestableUserService();

                // Act
                await service.TransferApiKeysScopedToUser(originalOwner, newOwner);

                // Assert
                service.MockEntitiesContext.Verify(
                    x => x.SaveChangesAsync(), 
                    hasUnscopedApiKeyCredential || hasApiKeyScopedToUserCredential ? Times.Once() : Times.Never());

                Assert.Equal(originalCredentialCount, originalOwner.Credentials.Count());

                Assert.Equal(
                    (hasUnscopedApiKeyCredential ? 1 : 0) + (hasApiKeyScopedToUserCredential ? 1 : 0), 
                    newOwner.Credentials.Count());

                AssertCredentialInOriginalOnly(externalCredential, originalOwner, newOwner, hasExternalCredential);
                AssertCredentialInOriginalOnly(passwordCredential, originalOwner, newOwner, hasPasswordCredential);
                AssertCredentialInOriginalOnly(scopedToDifferentUserApiKeyCredential, originalOwner, newOwner, hasApiKeyScopedToDifferentUser);

                AssertCredentialInNew(unscopedApiKeyCredential, originalOwner, newOwner, hasUnscopedApiKeyCredential);
                AssertCredentialInNew(scopedToUserApiKeyCredential, originalOwner, newOwner, hasApiKeyScopedToUserCredential);
            }

            private void AddFieldsToCredential(Credential credential, string description, string value, User originalOwner, DateTime? expiration)
            {
                credential.Description = description;
                credential.Value = value;
                credential.User = originalOwner;
                credential.UserKey = originalOwner.Key;

                if (expiration.HasValue)
                {
                    credential.ExpirationTicks = expiration.Value.Ticks;
                    credential.Expires = expiration.Value;
                }
            }

            private void AssertCredentialInOriginalOnly(Credential credential, User originalOwner, User newOwner, bool hasCredential)
            {
                var credentialEquals = CredentialEqualsFunc(credential);
                Assert.Equal(hasCredential, originalOwner.Credentials.Any(
                    hasCredential ? CredentialEqualsWithOwnerFunc(credential, originalOwner) : CredentialEqualsFunc(credential)));
                Assert.False(newOwner.Credentials.Any(CredentialEqualsFunc(credential)));
            }

            private void AssertCredentialInNew(Credential credential, User originalOwner, User newOwner, bool hasCredential)
            {
                Assert.Equal(hasCredential, originalOwner.Credentials.Any(
                    hasCredential ? CredentialEqualsWithOwnerFunc(credential, originalOwner) : CredentialEqualsFunc(credential)));
                Assert.Equal(hasCredential, newOwner.Credentials.Any(
                    hasCredential ? CredentialEqualsWithOwnerAndScopeFunc(credential, newOwner, originalOwner) : CredentialEqualsFunc(credential)));
            }

            private bool CredentialEquals(Credential expected, Credential actual)
            {
                return
                    expected.Description == actual.Description &&
                    expected.ExpirationTicks == actual.ExpirationTicks &&
                    expected.Expires == actual.Expires &&
                    expected.Type == actual.Type &&
                    expected.Value == actual.Value;
            }

            private bool CredentialEqualsWithOwner(Credential expected, Credential actual, User owner)
            {
                return CredentialEquals(expected, actual) &&
                    owner == actual.User &&
                    owner.Key == actual.UserKey;
            }

            private bool CredentialEqualsWithOwnerAndScope(Credential expected, Credential actual, User owner, User scopeOwner)
            {
                return CredentialEqualsWithOwner(expected, actual, owner) &&
                    expected.Scopes.All(s => s.Owner == scopeOwner && s.OwnerKey == scopeOwner.Key);
            }

            private Func<Credential, bool> CredentialEqualsFunc(Credential expected)
            {
                return (c) => CredentialEquals(expected, c);
            }

            private Func<Credential, bool> CredentialEqualsWithOwnerFunc(Credential expected, User owner)
            {
                return (c) => CredentialEqualsWithOwner(expected, c, owner);
            }

            private Func<Credential, bool> CredentialEqualsWithOwnerAndScopeFunc(Credential expected, User owner, User scopeOwner)
            {
                return (c) => CredentialEqualsWithOwnerAndScope(expected, c, owner, scopeOwner);
            }
        }

        public class TheAddOrganizationAccountMethod
        {
            private const string OrgName = "myOrg";
            private const string OrgEmail = "myOrg@myOrg.com";
            private const string AdminName = "orgAdmin";

            private static DateTime OrgCreatedUtc = new DateTime(2018, 2, 21);

            private TestableUserService _service = new TestableUserService();

            [Fact]
            public async Task WithUsernameConflict_ThrowsEntityException()
            {
                var conflictUsername = "ialreadyexist";

                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(new[] { new User(conflictUsername) }.MockDbSet().Object);

                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization(orgName: conflictUsername));
                Assert.Equal(String.Format(CultureInfo.CurrentCulture, Strings.UsernameNotAvailable, conflictUsername), exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Never());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WithEmailConflict_ThrowsEntityException()
            {
                var conflictEmail = "ialreadyexist@existence.com";

                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(new[] { new User("user") { EmailAddress = conflictEmail } }.MockDbSet().Object);

                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization(orgEmail: conflictEmail));
                Assert.Equal(String.Format(CultureInfo.CurrentCulture, Strings.EmailAddressBeingUsed, conflictEmail), exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Never());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WhenAdminHasNoTenant_ThrowsEntityException()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                var adminUsername = "adminWithNoTenant";
                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization(admin: new User(adminUsername)));
                Assert.Equal(String.Format(CultureInfo.CurrentCulture, Strings.Organizations_AdminAccountDoesNotHaveTenant, adminUsername), exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Once());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Never());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WhenSubscribingToPolicyFails_ThrowsUserSafeException()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                _service.MockSecurityPolicyService
                    .Setup(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false))
                    .Returns(Task.FromResult(false));

                var exception = await Assert.ThrowsAsync<EntityException>(() => InvokeAddOrganization());
                Assert.Equal(Strings.DefaultUserSafeExceptionMessage, exception.Message);

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Once());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Once());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never());
            }

            [Fact]
            public async Task WhenSubscribingToPolicySucceeds_ReturnsNewOrg()
            {
                _service.MockEntitiesContext
                    .Setup(x => x.Users)
                    .Returns(Enumerable.Empty<User>().MockDbSet().Object);

                _service.MockSecurityPolicyService
                    .Setup(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false))
                    .Returns(Task.FromResult(true));

                var org = await InvokeAddOrganization();

                Assert.Equal(OrgName, org.Username);
                Assert.Equal(OrgEmail, org.UnconfirmedEmailAddress);
                Assert.Equal(OrgCreatedUtc, org.CreatedUtc);
                Assert.True(org.EmailAllowed);
                Assert.True(org.NotifyPackagePushed);
                Assert.True(!string.IsNullOrEmpty(org.EmailConfirmationToken));

                // Both the organization and the admin must have a membership to each other.
                Func<Membership, bool> hasMembership = m => m.Member.Username == AdminName && m.Organization.Username == OrgName && m.IsAdmin;
                Assert.True(
                    org.Members.Any(
                        m => hasMembership(m) && m.Member.Organizations.Any(hasMembership)));

                _service.MockOrganizationRepository.Verify(x => x.InsertOnCommit(It.IsAny<Organization>()), Times.Once());
                _service.MockSecurityPolicyService.Verify(sp => sp.SubscribeAsync(It.IsAny<User>(), It.IsAny<IUserSecurityPolicySubscription>(), false), Times.Once());
                _service.MockEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once());
            }

            private Task<Organization> InvokeAddOrganization(string orgName = OrgName, string orgEmail = OrgEmail, User admin = null)
            {
                // Arrange
                admin = admin ?? new User(AdminName)
                {
                    Credentials = new Credential[] {
                        new CredentialBuilder().CreateExternalCredential(
                            issuer: "AzureActiveDirectory",
                            value: "abc123",
                            identity: "Admin",
                            tenantId: "zyx987")
                    }
                };

                _service.MockDateTimeProvider
                    .Setup(x => x.UtcNow)
                    .Returns(OrgCreatedUtc);

                // Act
                return _service.AddOrganizationAsync(orgName, orgEmail, admin);
            }
        }
    }
}

