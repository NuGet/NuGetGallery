// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Framework;
using NuGetGallery.Auditing;
using Xunit;
using NuGetGallery.TestUtils;
using Moq;
using System.Data.SqlClient;
using System.Data;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery
{
    public class UserServiceFacts
    {
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

        public class TheTransformToOrganizationAccountMethod
        {
            [Fact]
            public async Task WhenAccountIsNull_ThrowsArgNullException()
            {
                await ContractAssert.ThrowsArgNullAsync(
                    async () => await new TestableUserService().TransformToOrganizationAccount(null, new User("admin"), "token"),
                    "accountToTransform");
            }

            [Fact]
            public async Task WhenAdminIsNull_ThrowsArgNullException()
            {
                await ContractAssert.ThrowsArgNullAsync(
                    async () => await new TestableUserService().TransformToOrganizationAccount(new User("account"), null, "token"),
                    "adminUser");
            }

            [Theory]
            [InlineData("")]
            [InlineData(" ")]
            [InlineData(null)]
            public async Task WhenTokenIsMissing_ThrowsArgException(string token)
            {
                await ContractAssert.ThrowsArgNullAsync(
                    async () => await new TestableUserService().TransformToOrganizationAccount(new User("account"), new User("admin"), token),
                    "token");
            }

            [Fact]
            public async Task WhenNoTenant_ThrowsTransformAccountException()
            {
                // Arrange
                var account = new User("Account");
                var admin = new User("Admin");
                var service = new TestableUserService();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<TransformAccountException>(
                    async () => await service.TransformToOrganizationAccount(account, admin, "token"));
                Assert.Equal(exception.Message, Strings.TransformAccount_AdminDoesNotHaveTenantId);
            }

            [Fact]
            public async Task WhenSqlException_ThrowsTransformAccountException()
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                var admin = new User("Admin");
                admin.Credentials.Add(
                    new CredentialBuilder().CreateExternalCredential(
                        issuer: "MicrosoftAccount",
                        value: "abc123",
                        identity: "Admin",
                        tenantId: "zyx987"));

                service.MockDatabase
                    .Setup(db => db.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .ThrowsAsync(new DataException());

                // Act & Assert
                var exception = await Assert.ThrowsAsync<TransformAccountException>(
                    async () => await service.TransformToOrganizationAccount(account, admin, "token"));
                Assert.Equal(exception.Message, Strings.TransformAccount_DatabaseError);
            }

            [Fact]
            public async Task WhenSqlResultIsZero_ThrowsTransformAccountException()
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                var admin = new User("Admin");
                admin.Credentials.Add(
                    new CredentialBuilder().CreateExternalCredential(
                        issuer: "MicrosoftAccount",
                        value: "abc123",
                        identity: "Admin",
                        tenantId: "zyx987"));

                service.MockDatabase
                    .Setup(db => db.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .Returns(Task.FromResult(0));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<TransformAccountException>(
                    async () => await service.TransformToOrganizationAccount(account, admin, "token"));
                Assert.Equal(exception.Message, Strings.TransformAccount_SaveFailed);
            }

            [Fact]
            public async Task WhenSqlResultIsOne_ReturnsSuccess()
            {
                // Arrange
                var service = new TestableUserService();
                var account = new User("Account");
                var admin = new User("Admin");
                admin.Credentials.Add(
                    new CredentialBuilder().CreateExternalCredential(
                        issuer: "MicrosoftAccount",
                        value: "abc123",
                        identity: "Admin",
                        tenantId: "zyx987"));

                service.MockDatabase
                    .Setup(db => db.ExecuteSqlCommandAsync(It.IsAny<string>(), It.IsAny<object[]>()))
                    .Returns(Task.FromResult(1));

                // Act
                await service.TransformToOrganizationAccount(account, admin, "token");
            }
        }
    }
}

