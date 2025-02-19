// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Infrastructure.Mail.Requests;
using Xunit;

namespace NuGetGallery
{
    public class MarkdownMessageServiceFacts
    {
        private static readonly MailAddress TestGalleryOwner = TestableMarkdownMessageService.TestGalleryOwner;
        private static readonly MailAddress TestGalleryNoReplyAddress = TestableMarkdownMessageService.TestGalleryNoReplyAddress;

        public class TheReportPackageRequestMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailToGalleryOwner()
            {
                // Arrange
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1"
                };

                var urlHelper = TestUtility.MockUrlHelper();
                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var request = new ReportPackageRequest
                {
                    FromAddress = from,
                    Message = "Abuse!",
                    Package = package,
                    Reason = "Reason!",
                    RequestingUser = null,
                    Signature = "Joe Schmoe",
                    PackageUrl = urlHelper.Package(package.PackageRegistration.Id, false),
                    PackageVersionUrl = urlHelper.Package(package.PackageRegistration.Id, package.Version, false),
                    RequestingUserUrl = null
                };

                var reportAbuseMessage = new ReportAbuseMessage(
                    configurationService.Current,
                    request,
                    alreadyContactedOwners: true);

                // Act
                await messageService.SendMessageAsync(reportAbuseMessage);
                var message = messageService.MockMailSender.Sent.Last();

                // Assert
                Assert.Equal(TestGalleryOwner, message.To.Single());
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(from, message.ReplyToList.Single());
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
                Assert.Contains("Reason!", message.Body);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("too (legit@example.com)", message.Body);
                Assert.Contains("smangit", message.Body);
                Assert.Contains("1.42.0.1", message.Body);
                Assert.Contains("Yes", message.Body);
            }

            [Fact]
            public async Task WillCopySenderIfAsked()
            {
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1",
                };

                var urlHelper = TestUtility.MockUrlHelper();

                var requestingUser = new User
                {
                    Username = "Joe Schmoe",
                    EmailAddress = "joe@example.com"
                };
                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var request = new ReportPackageRequest
                {
                    FromAddress = from,
                    Message = "Abuse!",
                    Package = package,
                    Reason = "Reason!",
                    RequestingUser = requestingUser,
                    Signature = "Joe Schmoe",
                    PackageUrl = urlHelper.Package(package.PackageRegistration.Id, false),
                    PackageVersionUrl = urlHelper.Package(package.PackageRegistration.Id, package.Version, false),
                    RequestingUserUrl = urlHelper.User(requestingUser, 1, false),
                    CopySender = true,
                };

                var reportAbuseMessage = new ReportAbuseMessage(
                    configurationService.Current,
                    request,
                    alreadyContactedOwners: true);

                await messageService.SendMessageAsync(reportAbuseMessage);

                var message = messageService.MockMailSender.Sent.Single();
                Assert.Equal(TestGalleryOwner, message.To.Single());
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(request.FromAddress, message.ReplyToList.Single());
                Assert.Equal(request.FromAddress, message.CC.Single());
                Assert.DoesNotContain("Owners", message.Body);
            }
        }

        public class TheContactOwnersMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendCopyToSenderIfAsked()
            {
                // arrange
                var packageId = "smangit";
                var packageVersion = "1.0.0";
                var fromAddress = "smangit@example.com";
                var fromName = "flossy";
                var ownerAddress = "yung@example.com";
                var ownerAddress2 = "flynt@example.com";

                var from = new MailAddress(fromAddress, fromName);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { EmailAddress = ownerAddress, EmailAllowed = true },
                            new User { EmailAddress = ownerAddress2, EmailAllowed = true }
                        }
                    },
                    Version = packageVersion
                };

                var configuration = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configuration);

                var contactOwnersMessage = new ContactOwnersMessage(
                    configuration.Current,
                    from,
                    package,
                    "http://someurl/",
                    "Test message",
                    "http://someotherurl/");

                // act
                await messageService.SendMessageAsync(contactOwnersMessage, copySender: true, discloseSenderAddress: false);
                var messages = messageService.MockMailSender.Sent;

                // assert
                Assert.Equal(2, messages.Count);
                Assert.Equal(package.PackageRegistration.Owners.Count, messages[0].To.Count);
                Assert.Single(messages[1].To);
                Assert.Equal(ownerAddress, messages[0].To[0].Address);
                Assert.Equal(ownerAddress2, messages[0].To[1].Address);
                Assert.Equal(messages[1].ReplyToList.Single(), messages[1].To.First());
                Assert.Equal(TestGalleryOwner, messages[0].From);
                Assert.Equal(TestGalleryOwner, messages[1].From);
                Assert.Equal(fromAddress, messages[0].ReplyToList.Single().Address);
                Assert.Equal(fromAddress, messages[1].ReplyToList.Single().Address);
                Assert.Empty(messages[0].CC);
                Assert.Empty(messages[1].CC);
            }

            [Fact]
            public async Task WillSendEmailToAllOwners()
            {
                var id = "smangit";
                var version = "1.0.0";
                var owner1Email = "yung@example.com";
                var owner2Email = "flynt@example.com";
                var userUsername = "flossy";
                var userEmail = "smangit@example.com";
                var from = new MailAddress(userEmail, userUsername);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = id,
                        Owners = new[]
                        {
                            new User { EmailAddress = owner1Email, EmailAllowed = true },
                            new User { EmailAddress = owner2Email, EmailAllowed = true }
                        }
                    },
                    Version = version
                };

                var configuration = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configuration);

                var contactOwnersMessage = new ContactOwnersMessage(
                    configuration.Current,
                    from,
                    package,
                    "http://packageUrl/",
                    "Test message",
                    "http://emailSettingsUrl/");

                await messageService.SendMessageAsync(contactOwnersMessage);
                var message = messageService.MockMailSender.Sent.Single();

                Assert.Equal(owner1Email, message.To[0].Address);
                Assert.Equal(owner2Email, message.To[1].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(userEmail, message.ReplyToList.Single().Address);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Message for owners of the package '{id}'", message.Subject);
                Assert.Contains("Test message", message.Body);
                Assert.Contains(
                    $"User {userUsername} &lt;{userEmail}&gt; sends the following message to the owners of Package '[{id} {version}]({contactOwnersMessage.PackageUrl})'.",
                    message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailToOwnerThatOptsOut()
            {
                // arrange
                var packageId = "smangit";
                var packageVersion = "1.0.0";
                var fromAddress = "smangit@example.com";
                var fromName = "flossy";
                var ownerAddress = "yung@example.com";
                var ownerAddress2 = "flynt@example.com";

                var from = new MailAddress(fromAddress, fromName);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { EmailAddress = ownerAddress, EmailAllowed = true },
                            new User { EmailAddress = ownerAddress2, EmailAllowed = false }
                        }
                    },
                    Version = packageVersion
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);

                var contactOwnersMessage = new ContactOwnersMessage(
                    configurationService.Current,
                    from,
                    package,
                    "http://someurl/",
                    "Test message",
                    "http://someotherurl/");

                await messageService.SendMessageAsync(contactOwnersMessage);
                var message = messageService.MockMailSender.Sent.Single();

                // assert
                Assert.Equal(ownerAddress, message.To[0].Address);
                Assert.Single(message.To);
            }

            [Fact]
            public async Task WillNotAttemptToSendIfNoOwnersAllow()
            {
                // arrange
                var packageId = "smangit";
                var packageVersion = "1.0.0";
                var fromAddress = "smangit@example.com";
                var fromName = "flossy";
                var ownerAddress = "yung@example.com";
                var ownerAddress2 = "flynt@example.com";

                var from = new MailAddress(fromAddress, fromName);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { EmailAddress = ownerAddress, EmailAllowed = false },
                            new User { EmailAddress = ownerAddress2, EmailAllowed = false }
                        }
                    },
                    Version = packageVersion
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);

                var contactOwnersMessage = new ContactOwnersMessage(
                    configurationService.Current,
                    from,
                    package,
                    "http://someurl/",
                    "Test message",
                    "http://someotherurl/");

                await messageService.SendMessageAsync(contactOwnersMessage);

                // assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }

            [Fact]
            public async Task WillNotSendCopyToSenderIfNoOwnersAllow()
            {
                // arrange
                var packageId = "smangit";
                var packageVersion = "1.0.0";
                var fromAddress = "smangit@example.com";
                var fromName = "flossy";
                var ownerAddress = "yung@example.com";
                var ownerAddress2 = "flynt@example.com";

                var from = new MailAddress(fromAddress, fromName);
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = packageId,
                        Owners = new[]
                        {
                            new User { EmailAddress = ownerAddress, EmailAllowed = false },
                            new User { EmailAddress = ownerAddress2, EmailAllowed = false }
                        }
                    },
                    Version = packageVersion
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var contactOwnersMessage = new ContactOwnersMessage(
                    configurationService.Current,
                    from,
                    package,
                    "http://someurl/",
                    "Test message",
                    "http://someotherurl/");
                await messageService.SendMessageAsync(contactOwnersMessage);

                // assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheNewAccountMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmailToNewUser(bool isOrganization)
            {
                var unconfirmedEmailAddress = "unconfirmed@unconfirmed.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.UnconfirmedEmailAddress = unconfirmedEmailAddress;

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var newAccountEmailMessage = new NewAccountMessage(configurationService.Current, user, "http://example.com/confirmation-token-url");

                await messageService.SendMessageAsync(newAccountEmailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(unconfirmedEmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please verify your account", message.Subject);
                Assert.Contains($"Thank you for {(isOrganization ? "creating an organization on the" : "registering with the")} {TestGalleryOwner.DisplayName}.", message.Body);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }

        public class TheEmailChangeConfirmationMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmail(bool isOrganization)
            {
                var unconfirmedEmailAddress = "unconfirmed@unconfirmed.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.UnconfirmedEmailAddress = unconfirmedEmailAddress;
                var tokenUrl = "http://example.com/confirmation-token-url";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailChangeConfirmationMessage = new EmailChangeConfirmationMessage(configurationService.Current, user, tokenUrl);

                await messageService.SendMessageAsync(emailChangeConfirmationMessage);

                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.UnconfirmedEmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please verify your {(isOrganization ? "organization's" : "account's")} new email address", message.Subject);
                Assert.Contains(tokenUrl, message.Body);
            }
        }

        public class TheEmailChangeNoticeToPreviousEmailAddressMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmail(bool isOrganization)
            {
                var newEmail = "new@email.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.EmailAddress = newEmail;
                var oldEmail = "old@email.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailChangeMessage = new EmailChangeNoticeToPreviousEmailAddressMessage(configurationService.Current, user, oldEmail);

                await messageService.SendMessageAsync(emailChangeMessage);
                var message = messageService.MockMailSender.Sent.Last();

                var accountString = isOrganization ? "organization" : "account";

                Assert.Equal(oldEmail, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Recent changes to your {accountString}'s email", message.Subject);
                Assert.Contains($"The email address associated with your {TestGalleryOwner.DisplayName} {accountString} was recently changed from _{oldEmail}_ to _{user.EmailAddress}_.", message.Body);
            }
        }

        public class ThePackageOwnershipRequestMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsPackageOwnerRequestConfirmationUrl(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "new-owner@example.com";
                to.EmailAllowed = true;

                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string packageUrl = "http://nuget.local/packages/CoolStuff";
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                const string rejectionUrl = "http://example.com/rejection-token-url";
                const string userMessage = "Hello World!";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageOwnershipRequestMessage = new PackageOwnershipRequestMessage(
                    configurationService.Current,
                    from,
                    to,
                    package,
                    packageUrl,
                    confirmationUrl,
                    rejectionUrl,
                    userMessage,
                    string.Empty);
                await messageService.SendMessageAsync(packageOwnershipRequestMessage);

                var message = messageService.MockMailSender.Sent.Last();

                var yourString = isOrganization ? "your organization" : "you";

                if (isOrganization)
                {
                    AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(message, to as Organization);
                }
                else
                {
                    Assert.Equal(to.EmailAddress, message.To[0].Address);
                }
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(from.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Package ownership request for '{package.Id}'", message.Subject);
                Assert.Contains($"The user '{from.Username}' added the following message for you", message.Body);
                Assert.Contains(userMessage, message.Body);
                Assert.Contains(confirmationUrl, message.Body);
                Assert.Contains(rejectionUrl, message.Body);
                Assert.Contains($"The user '{from.Username}' would like to add {(to is Organization ? "your organization" : "you")} as an owner of the package ['{package.Id}']({packageUrl}).", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsPackageOwnerRequestConfirmationUrlWithoutUserMessage(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "new-owner@example.com";
                to.EmailAllowed = true;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string packageUrl = "http://nuget.local/packages/CoolStuff";
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                const string rejectionUrl = "http://example.com/rejection-token-url";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageOwnershipRequestMessage = new PackageOwnershipRequestMessage(
                    configurationService.Current,
                    from,
                    to,
                    package,
                    packageUrl,
                    confirmationUrl,
                    rejectionUrl,
                    string.Empty,
                    string.Empty);
                await messageService.SendMessageAsync(packageOwnershipRequestMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.DoesNotContain("The user 'Existing' added the following message for you", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotSendRequestIfUserDoesNotAllowEmails(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "new-owner@example.com";
                to.EmailAllowed = false;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string packageUrl = "http://nuget.local/packages/CoolStuff";
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                const string rejectionUrl = "http://example.com/rejection-token-url";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageOwnershipRequestMessage = new PackageOwnershipRequestMessage(
                    configurationService.Current,
                    from,
                    to,
                    package,
                    packageUrl,
                    confirmationUrl,
                    rejectionUrl,
                    string.Empty,
                    string.Empty);
                await messageService.SendMessageAsync(packageOwnershipRequestMessage);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageOwnershipRequestInitiatedMessage
            : TestContainer
        {
            [Fact]
            public async Task SendsNotice()
            {
                var requestingOwner = new User("Existing") { EmailAddress = "existing-owner@example.com" };
                var receivingOwner = new User("Receiving")
                {
                    EmailAddress = "receiving-owner@example.com",
                    EmailAllowed = true
                };

                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var cancelUrl = "http://example.com/cancellation-url";

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestInitiatedMessage(configurationService.Current, requestingOwner, receivingOwner, newOwner, package, cancelUrl);
                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(receivingOwner.EmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(newOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Package ownership request for '{package.Id}'", message.Subject);
                Assert.Contains($"The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.", message.Body);
                Assert.Contains($"[{cancelUrl}]({cancelUrl})", message.Body);
            }

            [Fact]
            public async Task DoesNotSendNoticeIfUserDoesNotAllowEmails()
            {
                var requestingOwner = new User("Existing") { EmailAddress = "existing-owner@example.com" };
                var receivingOwner = new User("Receiving")
                {
                    EmailAddress = "receiving-owner@example.com",
                    EmailAllowed = false
                };

                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var cancelUrl = "http://example.com/cancellation-url";

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestInitiatedMessage(configurationService.Current, requestingOwner, receivingOwner, newOwner, package, cancelUrl);
                await messageService.SendMessageAsync(emailMessage);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageOwnershipRequestDeclinedMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsNotice(bool isOrganization)
            {
                var requestingOwner = isOrganization ? GetOrganizationWithRecipients() : new User();
                requestingOwner.Username = "Existing";
                requestingOwner.EmailAddress = "existing-owner@example.com";
                requestingOwner.EmailAllowed = true;
                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestDeclinedMessage(configurationService.Current, requestingOwner, newOwner, package);

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                var yourString = isOrganization ? "your organization's" : "your";

                if (isOrganization)
                {
                    AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(message, requestingOwner as Organization);
                }
                else
                {
                    Assert.Equal(requestingOwner.EmailAddress, message.To[0].Address);
                }
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(newOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Package ownership request for '{package.Id}' declined", message.Subject);
                Assert.Contains($"The user '{newOwner.Username}' has declined {(requestingOwner is Organization ? "your organization's" : "your")} request to add them as an owner of the package '{package.Id}'.", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotSendNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                var requestingOwner = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                requestingOwner.Username = "Existing";
                requestingOwner.EmailAddress = "existing-owner@example.com";
                requestingOwner.EmailAllowed = false;
                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestDeclinedMessage(configurationService.Current, requestingOwner, newOwner, package);
                await messageService.SendMessageAsync(emailMessage);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageOwnershipRequestCanceledMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsNotice(bool isOrganization)
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var newOwner = isOrganization ? GetOrganizationWithRecipients() : new User();
                newOwner.Username = "Noob";
                newOwner.EmailAddress = "new-owner@example.com";
                newOwner.EmailAllowed = true;
                var package = new PackageRegistration { Id = "CoolStuff" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestCanceledMessage(configurationService.Current, requestingOwner, newOwner, package);
                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                var yourString = isOrganization ? "your organization" : "you";

                if (isOrganization)
                {
                    AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(message, newOwner as Organization);
                }
                else
                {
                    Assert.Equal(newOwner.EmailAddress, message.To[0].Address);
                }
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(requestingOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Package ownership request for '{package.Id}' cancelled", message.Subject);
                Assert.Contains($"The user '{requestingOwner.Username}' has cancelled their request for {(newOwner is Organization ? "your organization" : "you")} to be added as an owner of the package '{package.Id}'.", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotSendNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };

                var newOwner = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                newOwner.Username = "Noob";
                newOwner.EmailAddress = "new-owner@example.com";
                newOwner.EmailAllowed = false;
                var package = new PackageRegistration { Id = "CoolStuff" };

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner
                };
                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnershipRequestCanceledMessage(configurationService.Current, requestingOwner, newOwner, package);
                await messageService.SendMessageAsync(emailMessage);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageOwnerAddedMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsPackageOwnerAddedNotice(bool isOrganization)
            {
                // Arrange
                var toUser = isOrganization ? GetOrganizationWithRecipients() : new User();
                toUser.Username = "Existing";
                toUser.EmailAddress = "existing-owner@example.com";
                toUser.EmailAllowed = true;
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var packageUrl = "packageUrl";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnerAddedMessage(
                    configurationService.Current,
                    toUser,
                    newUser,
                    package,
                    packageUrl);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();
                if (isOrganization)
                {
                    AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(message, toUser as Organization);
                }
                else
                {
                    Assert.Equal(toUser.EmailAddress, message.To[0].Address);
                }
                Assert.Equal("noreply@example.com", TestGalleryNoReplyAddress.Address);
                Assert.Contains($"Package ownership update for '{package.Id}'", message.Subject);
                Assert.Contains($"User '{newUser.Username}' is now an owner of the package ['{package.Id}']({packageUrl}).", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotSendPackageOwnerAddedNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                // Arrange
                var toUser = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                toUser.Username = "Existing";
                toUser.EmailAddress = "existing-owner@example.com";
                toUser.EmailAllowed = false;
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = false };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnerAddedMessage(
                    configurationService.Current,
                    toUser,
                    newUser,
                    package,
                    "packageUrl");

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageOwnerRemovedMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SendsPackageOwnerRemovedNotice(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "old-owner@example.com";
                to.EmailAllowed = true;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnerRemovedMessage(configurationService.Current, from, to, package);
                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                if (isOrganization)
                {
                    AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(message, to as Organization);
                }
                else
                {
                    Assert.Equal(to.EmailAddress, message.To[0].Address);
                }
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(from.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Contains($"Package ownership removal for '{package.Id}'", message.Subject);
                Assert.Contains($"The user '{from.Username}' removed {(isOrganization ? "your organization" : "you")} as an owner of the package '{package.Id}'.", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task DoesNotSendRemovedNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "old-owner@example.com";
                to.EmailAllowed = false;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new PackageOwnerRemovedMessage(configurationService.Current, from, to, package);
                await messageService.SendMessageAsync(emailMessage);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePasswordResetInstructionsMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendInstructions()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "too" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var passwordResetInstructionsMessage = new PasswordResetInstructionsMessage(
                    configurationService.Current,
                    user,
                    "http://example.com/pwd-reset-token-url",
                    forgotPassword: true);

                await messageService.SendMessageAsync(passwordResetInstructionsMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please reset your password.", message.Subject);
                Assert.Contains("Click the following link within the next", message.Body);
                Assert.Contains("http://example.com/pwd-reset-token-url", message.Body);
            }
        }

        public class TheCredentialRemovedMessage
            : TestContainer
        {
            private AuthenticationService _authenticationService;

            public TheCredentialRemovedMessage()
            {
                _authenticationService = GetService<AuthenticationService>();
            }

            [Fact]
            public async Task UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                const string MicrosoftAccountCredentialName = "Microsoft account";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialRemovedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, MicrosoftAccountCredentialName), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialRemoved_Body, MicrosoftAccountCredentialName), message.Body);
            }

            [Fact]
            public async Task UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreatePasswordCredential("bogus");

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialRemovedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_Password), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialRemoved_Body, Strings.CredentialType_Password), message.Body);
            }

            [Fact]
            public async Task ApiKeyRemovedMessageIsCorrect()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1)).WithDefaultScopes();
                cred.Description = "new api key";
                cred.User = user;

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialRemovedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_ApiKey), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_ApiKeyRemoved_Body, cred.Description), message.Body);
            }
        }

        public class TheCredentialAddedMessage
            : TestContainer
        {
            private AuthenticationService _authenticationService;

            public TheCredentialAddedMessage()
            {
                _authenticationService = GetService<AuthenticationService>();
            }

            [Fact]
            public async Task UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                const string MicrosoftAccountCredentialName = "Microsoft account";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialAddedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, MicrosoftAccountCredentialName), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialAdded_Body, MicrosoftAccountCredentialName), message.Body);
            }

            [Fact]
            public async Task UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreatePasswordCredential("bogus");

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialAddedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_Password), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialAdded_Body, Strings.CredentialType_Password), message.Body);
            }

            [Fact]
            public async Task ApiKeyAddedMessageIsCorrect()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1)).WithDefaultScopes();
                cred.Description = "new api key";
                cred.User = user;

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new CredentialAddedMessage(
                    configurationService.Current,
                    user,
                    _authenticationService.DescribeCredential(cred).GetCredentialTypeInfo());

                await messageService.SendMessageAsync(emailMessage);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_ApiKey), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_ApiKeyAdded_Body, cred.Description), message.Body);
            }
        }

        public class ThePackageAddedMessage
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public async Task WillSendEmailToAllOwners(string version)
            {
                // Arrange
                var nugetVersion = new NuGetVersion(version);
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = true }
                    }
                };
                var package = new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";

                var packageAddedMessage = new PackageAddedMessage(
                    configurationService.Current,
                    package,
                    packageUrl,
                    supportUrl,
                    "https://localhost/account",
                    warningMessages: null);

                // Act
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                await messageService.SendMessageAsync(packageAddedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package published - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was recently published on {TestGalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({supportUrl}).", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailToOwnerThatOptsOut()
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = false }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);

                var packageAddedMessage = new PackageAddedMessage(
                    configurationService.Current,
                    package,
                    "http://dummy1",
                    "http://dummy2",
                    "http://dummy3",
                    warningMessages: null);

                // Act
                await messageService.SendMessageAsync(packageAddedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Single(message.To);
            }

            [Fact]
            public async Task WillNotAttemptToSendIfNoOwnersAllow()
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = false },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);

                var packageAddedMessage = new PackageAddedMessage(
                    configurationService.Current,
                    package,
                    "http://dummy1",
                    "http://dummy2",
                    "http://dummy3",
                    warningMessages: null);

                // Act
                await messageService.SendMessageAsync(packageAddedMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class ThePackageValidationFailedMessage
            : TestContainer
        {
            public static IEnumerable<object[]> WillSendEmailToAllOwners_Data
            {
                get
                {
                    foreach (var user1PushAllowed in new[] { false, true })
                    {
                        foreach (var user2PushAllowed in new[] { false, true })
                        {
                            foreach (var user1EmailAllowed in new[] { false, true })
                            {
                                foreach (var user2EmailAllowed in new[] { false, true })
                                {
                                    foreach (var validationIssue in new[] {
                                        ValidationIssue.AuthorAndRepositoryCounterSignaturesNotSupported,
                                        ValidationIssue.AuthorCounterSignaturesNotSupported,
                                        ValidationIssue.OnlyAuthorSignaturesSupported,
                                        ValidationIssue.OnlySignatureFormatVersion1Supported,
                                        ValidationIssue.PackageIsNotSigned,
                                        ValidationIssue.PackageIsSigned,
                                        ValidationIssue.PackageIsZip64,
                                        ValidationIssue.Unknown,
                                        new ClientSigningVerificationFailure("NU9999", "test message"),
                                        new UnauthorizedCertificateFailure("asdfasdfasdf")})
                                    {
                                        yield return MemberDataHelper.AsData(validationIssue, user1PushAllowed, user2PushAllowed, user1EmailAllowed, user2EmailAllowed);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WillSendEmailToAllOwners_Data))]
            public async Task WillSendEmailToAllOwners(ValidationIssue validationIssue, bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = user1PushAllowed, EmailAllowed = user1EmailAllowed },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = user2PushAllowed, EmailAllowed = user2EmailAllowed }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var packageValidationSet = new PackageValidationSet()
                {
                    PackageValidations = new[]
                    {
                        new PackageValidation()
                        {
                            PackageValidationIssues = new[]
                            {
                                new PackageValidationIssue()
                                {
                                    Key = 0,
                                    IssueCode = validationIssue.IssueCode,
                                    Data = validationIssue.Serialize()
                                }
                            }
                        }
                    }
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = "https://packageUrl";
                var supportUrl = "https://supportUrl";
                var announcementsUrl = "https://announcementsUrl";
                var twitterUrl = "https://twitterUrl";

                var packageValidationFailedMessage = new PackageValidationFailedMessage(
                    configurationService.Current,
                    package,
                    packageValidationSet,
                    packageUrl,
                    supportUrl,
                    announcementsUrl,
                    twitterUrl);

                // Act
                await messageService.SendMessageAsync(packageValidationFailedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(2, message.To.Count);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package validation failed - {packageRegistration.Id} {package.Version}", message.Subject);
                Assert.Contains($"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) failed validation because of the following reason(s):", message.Body);
                Assert.Contains(ParseValidationIssue(validationIssue, announcementsUrl, twitterUrl), message.Body);
                Assert.Contains($"Your package was not published on {TestGalleryOwner.DisplayName} and is not available for consumption.", message.Body);

                if (validationIssue.IssueCode == ValidationIssueCode.Unknown)
                {
                    Assert.Contains($"Please [contact support]({supportUrl}) to help fix your package.", message.Body);
                }
                else
                {
                    Assert.Contains("You can reupload your package once you've fixed the issue with it.", message.Body);
                }
            }

            private static string ParseValidationIssue(ValidationIssue validationIssue, string announcementsUrl, string twitterUrl)
            {
                switch (validationIssue.IssueCode)
                {
                    case ValidationIssueCode.PackageIsSigned:
                        return $"This package could not be published since it is signed. We do not accept signed packages at this moment. To be notified about package signing and more, watch our [Announcements]({announcementsUrl}) page or follow us on [Twitter]({twitterUrl}).";
                    case ValidationIssueCode.ClientSigningVerificationFailure:
                        var clientIssue = (ClientSigningVerificationFailure)validationIssue;
                        return $"**{clientIssue.ClientCode}**: {clientIssue.ClientMessage}";
                    case ValidationIssueCode.PackageIsZip64:
                        return "Zip64 packages are not supported.";
                    case ValidationIssueCode.OnlyAuthorSignaturesSupported:
                        return "Signed packages must only have an author signature. Other signature types are not supported.";
                    case ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported:
                        return "Author countersignatures and repository countersignatures are not supported.";
                    case ValidationIssueCode.OnlySignatureFormatVersion1Supported:
                        return "**NU3007:** Package signatures must have format version 1.";
                    case ValidationIssueCode.AuthorCounterSignaturesNotSupported:
                        return "Author countersignatures are not supported.";
                    case ValidationIssueCode.PackageIsNotSigned:
                        return "This package must be signed with a registered certificate. [Read more...](https://aka.ms/nuget-signed-ref)";
                    case ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate:
                        var certIssue = (UnauthorizedCertificateFailure)validationIssue;
                        return $"The package was signed, but the signing certificate (SHA-1 thumbprint {certIssue.Sha1Thumbprint}) is not associated with your account. You must register this certificate to publish signed packages. [Read more...](https://aka.ms/nuget-signed-ref)";
                    default:
                        return "There was an unknown failure when validating your package.";
                }
            }
        }

        public class ThePackageValidationTakingTooLongMessage
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public async Task WillSendEmailToAllOwners(string version)
            {
                // Arrange
                var nugetVersion = new NuGetVersion(version);
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = true }
                    }
                };
                var package = new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var validationTakingTooLongMessage = new PackageValidationTakingTooLongMessage(configurationService.Current, package, packageUrl);

                // Act
                await messageService.SendMessageAsync(validationTakingTooLongMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package validation taking longer than expected - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"It is taking longer than expected for your package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) to get published." + Environment.NewLine + Environment.NewLine +
                    "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published." + Environment.NewLine + Environment.NewLine +
                    "Thank you for your patience.", message.Body);
            }

            public static IEnumerable<object[]> EmailSettingsCombinations
                => from u1pa in new[] { true, false }
                   from u2pa in new[] { true, false }
                   from u1ea in new[] { true, false }
                   from u2ea in new[] { true, false }
                   select new object[] { u1pa, u2pa, u1ea, u2ea };

            [Theory]
            [MemberData(nameof(EmailSettingsCombinations))]
            public async Task WillHonorPushSettings(bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = user1PushAllowed, EmailAllowed = user1EmailAllowed },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = user2PushAllowed, EmailAllowed = user2EmailAllowed }
                    }
                };
                var user1EmailShouldBeSent = user1PushAllowed;
                var user2EmailShouldBeSent = user2PushAllowed;
                int expectedNumberOfEmails = (user1EmailShouldBeSent ? 1 : 0) + (user2EmailShouldBeSent ? 1 : 0);
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var validationTakingTooLongMessage = new PackageValidationTakingTooLongMessage(configurationService.Current, package, "http://dummy1");

                // Act
                await messageService.SendMessageAsync(validationTakingTooLongMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.LastOrDefault();

                if (expectedNumberOfEmails == 0)
                {
                    Assert.Null(message);
                }
                else
                {
                    if (user1EmailShouldBeSent)
                    {
                        Assert.Contains("yung@example.com", message.To.Select(ma => ma.Address));
                    }
                    if (user2EmailShouldBeSent)
                    {
                        Assert.Contains("flynt@example.com", message.To.Select(ma => ma.Address));
                    }
                    Assert.Equal(expectedNumberOfEmails, message.To.Count);
                }
            }
        }

        public class ThePackageDeletedNoticeMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailToAllOwners()
            {
                // Arrange
                var nugetVersion = new NuGetVersion("3.1.0");
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com" },
                        new User { EmailAddress = "flynt@example.com" }
                    }
                };
                var package = new Package
                {
                    Version = "3.1.0",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                var emailMessage = new PackageDeletedNoticeMessage(
                    configurationService.Current,
                    package,
                    packageUrl,
                    supportUrl);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package deleted - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was just deleted from {TestGalleryOwner.DisplayName}. If this was not intended, please [contact support]({supportUrl}).", message.Body);
            }
        }

        public class TheOrganizationTransformRequestMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformRequestMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    profileUrl,
                    confirmationUrl,
                    rejectionUrl);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(adminUser.EmailAddress, message.To[0].Address);
                Assert.Equal(accountToTransform.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Organization transformation for account '{accountToTransform.Username}'", message.Subject);
                Assert.Contains($"We have received a request to transform account ['{accountToTransform.Username}']({profileUrl}) into an organization.", message.Body);
                Assert.Contains($"[{confirmationUrl}]({confirmationUrl})", message.Body);
                Assert.Contains($"[{rejectionUrl}]({rejectionUrl})", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformRequestMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    profileUrl,
                    confirmationUrl,
                    rejectionUrl);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationTransformInitiatedMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var organizationTransformInitiatedMessage = new OrganizationTransformInitiatedMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    cancelUrl);

                // Act
                await messageService.SendMessageAsync(organizationTransformInitiatedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Organization transformation for account '{accountToTransform.Username}'", message.Subject);
                Assert.Contains($"We have received a request to transform account '{accountToTransform.Username}' into an organization with user '{adminUser.Username}' as its admin.", message.Body);
                Assert.Contains($"[{cancelUrl}]({cancelUrl})", message.Body);
                Assert.Contains("If you did not request this change, please contact support by responding to this email.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var organizationTransformInitiatedMessage = new OrganizationTransformInitiatedMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    cancelUrl);

                // Act
                await messageService.SendMessageAsync(organizationTransformInitiatedMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationTransformAcceptedMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformAcceptedMessage(configurationService.Current, accountToTransform, adminUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Account '{accountToTransform.Username}' has been transformed into an organization", message.Subject);
                Assert.Contains($"Account '{accountToTransform.Username}' has been transformed into an organization with user '{adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformAcceptedMessage(configurationService.Current, accountToTransform, adminUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationTransformRejectedMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformRejectedMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    isCanceledByAdmin: true);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Transformation of account '{accountToTransform.Username}' has been cancelled", message.Subject);
                Assert.Contains($"Transformation of account '{accountToTransform.Username}' has been cancelled by user '{adminUser.Username}'.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationTransformRejectedMessage(
                    configurationService.Current,
                    accountToTransform,
                    adminUser,
                    isCanceledByAdmin: true);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMembershipRequestMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var adminUser = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var newUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestMessage(
                    configurationService.Current,
                    organization,
                    newUser,
                    adminUser,
                    isAdmin,
                    profileUrl,
                    confirmationUrl,
                    rejectionUrl);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(newUser.EmailAddress, message.To[0].Address);
                Assert.Equal(organization.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}'", message.Subject);
                Assert.Contains($"The user '{adminUser.Username}' would like you to become {membershipLevel} of their organization, ['{organization.Username}']({profileUrl}).", message.Body);
                Assert.Contains($"[{confirmationUrl}]({confirmationUrl})", message.Body);
                Assert.Contains($"[{rejectionUrl}]({rejectionUrl})", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestMessage(
                    configurationService.Current,
                    new Organization("transformers") { EmailAddress = "transformers@transformers.com" },
                    new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false },
                    new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" },
                    isAdmin,
                    "www.profile.com",
                    "www.confirm.com",
                    "www.rejection.com");

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMembershipRequestInitiatedMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = GetOrganizationWithRecipients();
                var requestingUser = new User("optimusprime") { EmailAddress = "optimusprime@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestInitiatedMessage(
                    configurationService.Current,
                    organization,
                    requestingUser,
                    pendingUser,
                    isAdmin);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                AssertMessageSentToAccountManagersOfOrganizationOnly(message, organization);

                Assert.Equal(requestingUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}'", message.Subject);
                Assert.Contains($"The user '{requestingUser.Username}' has requested that user '{pendingUser.Username}' be added as {(isAdmin ? "an administrator" : "a collaborator")} of organization '{organization.Username}'.", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var organization = GetOrganizationWithoutRecipients();
                var requestingUser = new User("optimusprime") { EmailAddress = "optimusprime@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestInitiatedMessage(
                    configurationService.Current,
                    organization,
                    requestingUser,
                    pendingUser,
                    isAdmin);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMembershipRequestDeclinedMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = GetOrganizationWithRecipients();
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestDeclinedMessage(configurationService.Current, organization, pendingUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                AssertMessageSentToAccountManagersOfOrganizationOnly(message, organization);

                Assert.Equal(pendingUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' declined", message.Subject);
                Assert.Contains($"The user '{pendingUser.Username}' has declined your request to become a member of your organization.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var organization = GetOrganizationWithoutRecipients();
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestDeclinedMessage(configurationService.Current, organization, pendingUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMembershipRequestCanceledMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestCanceledMessage(configurationService.Current, organization, pendingUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(pendingUser.EmailAddress, message.To[0].Address);
                Assert.Equal(organization.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' cancelled", message.Subject);
                Assert.Contains($"The request for you to become a member of '{organization.Username}' has been cancelled.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMembershipRequestCanceledMessage(configurationService.Current, accountToTransform, pendingUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMemberUpdatedMessage
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = true };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var membership = new Membership { Organization = organization, Member = member, IsAdmin = isAdmin };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMemberUpdatedMessage(configurationService.Current, organization, membership);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(organization.EmailAddress, message.To[0].Address);
                Assert.Equal(member.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                var membershipLevel = isAdmin ? "an administrator" : "a collaborator";
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership update for organization '{organization.Username}'", message.Subject);
                Assert.Contains($"The user '{member.Username}' is now {membershipLevel} of organization '{organization.Username}'.", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = false };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var membership = new Membership { Organization = organization, Member = member, IsAdmin = isAdmin };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMemberUpdatedMessage(configurationService.Current, organization, membership);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheOrganizationMemberRemovedMessage
            : TestContainer
        {
            [Fact]
            public async Task WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = true };
                var removedUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMemberRemovedMessage(configurationService.Current, organization, removedUser);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(organization.EmailAddress, message.To[0].Address);
                Assert.Equal(removedUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership update for organization '{organization.Username}'", message.Subject);
                Assert.Contains($"The user '{removedUser.Username}' is no longer a member of organization '{organization.Username}'.", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = false };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new OrganizationMemberRemovedMessage(configurationService.Current, organization, member);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheAccountDeleteNoticeMessage
            : TestContainer
        {
            [Fact]
            public async Task VerifyTheMessageBody()
            {
                // Arrange
                var userName = "deleteduser";
                var userEmailAddress = "onedeleteduser@hotmail.com";
                var userToDelete = new User(userName) { EmailAddress = userEmailAddress };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var emailMessage = new AccountDeleteNoticeMessage(configurationService.Current, userToDelete);

                // Act
                await messageService.SendMessageAsync(emailMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(userToDelete.EmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal("Account Deletion Request", message.Subject);
                Assert.Contains($"We received a request to delete your account {userName}.", message.Body);
            }
        }

        public class TheSymbolPackageValidationTakingTooLongMessage
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public async Task WillSendEmailToAllOwners(string version)
            {
                // Arrange
                var nugetVersion = new NuGetVersion(version);
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = true }
                    }
                };
                var package = new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 12
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var validationTakingTooLongMessage = new SymbolPackageValidationTakingTooLongMessage(configurationService.Current, symbolPackage, packageUrl);

                // Act
                await messageService.SendMessageAsync(validationTakingTooLongMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Symbol package validation taking longer than expected - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"It is taking longer than expected for your symbol package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) to get published." + Environment.NewLine + Environment.NewLine +
                    "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published." + Environment.NewLine + Environment.NewLine +
                    "Thank you for your patience.", message.Body);
            }
        }

        public class TheSymbolPackageValidationFailedMessage
            : TestContainer
        {
            public static IEnumerable<object[]> WillSendEmailToAllOwners_Data
            {
                get
                {
                    foreach (var user1PushAllowed in new[] { false, true })
                    {
                        foreach (var user2PushAllowed in new[] { false, true })
                        {
                            foreach (var user1EmailAllowed in new[] { false, true })
                            {
                                foreach (var user2EmailAllowed in new[] { false, true })
                                {
                                    foreach (var validationIssue in new[] {
                                        ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch,
                                        ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound,
                                        ValidationIssue.Unknown
                                       })
                                    {
                                        yield return MemberDataHelper.AsData(validationIssue, user1PushAllowed, user2PushAllowed, user1EmailAllowed, user2EmailAllowed);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(WillSendEmailToAllOwners_Data))]
            public async Task WillSendEmailToAllOwners(ValidationIssue validationIssue, bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = user1PushAllowed, EmailAllowed = user1EmailAllowed },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = user2PushAllowed, EmailAllowed = user2EmailAllowed }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration
                };
                packageRegistration.Packages.Add(package);

                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 12
                };

                var packageValidationSet = new PackageValidationSet()
                {
                    PackageValidations = new[]
                    {
                        new PackageValidation()
                        {
                            PackageValidationIssues = new[]
                            {
                                new PackageValidationIssue()
                                {
                                    Key = 0,
                                    IssueCode = validationIssue.IssueCode,
                                    Data = validationIssue.Serialize()
                                }
                            }
                        }
                    }
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = "https://packageUrl";
                var supportUrl = "https://supportUrl";
                var announcementsUrl = "https://announcementsUrl";
                var twitterUrl = "https://twitterUrl";

                var symbolPackageValidationFailedMessage = new SymbolPackageValidationFailedMessage(
                    configurationService.Current,
                    symbolPackage,
                    packageValidationSet,
                    packageUrl,
                    supportUrl,
                    announcementsUrl,
                    twitterUrl);

                // Act
                await messageService.SendMessageAsync(symbolPackageValidationFailedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(2, message.To.Count);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Symbol package validation failed - {packageRegistration.Id} {package.Version}", message.Subject);
                Assert.Contains($"The symbol package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) failed validation because of the following reason(s):", message.Body);
                Assert.Contains(ParseValidationIssue(validationIssue, announcementsUrl, twitterUrl), message.Body);
                Assert.Contains($"Your symbol package was not published on {TestGalleryOwner.DisplayName} and is not available for consumption.", message.Body);

                if (validationIssue.IssueCode == ValidationIssueCode.Unknown)
                {
                    Assert.Contains($"Please [contact support]({supportUrl}) to help.", message.Body);
                }
                else
                {
                    Assert.Contains("You can reupload your symbol package once you've fixed the issue with it.", message.Body);
                }
            }

            private static string ParseValidationIssue(ValidationIssue validationIssue, string announcementsUrl, string twitterUrl)
            {
                switch (validationIssue.IssueCode)
                {
                    case ValidationIssueCode.PackageIsSigned:
                        return $"This package could not be published since it is signed. We do not accept signed packages at this moment. To be notified about package signing and more, watch our [Announcements]({announcementsUrl}) page or follow us on [Twitter]({twitterUrl}).";
                    case ValidationIssueCode.ClientSigningVerificationFailure:
                        var clientIssue = (ClientSigningVerificationFailure)validationIssue;
                        return $"**{clientIssue.ClientCode}**: {clientIssue.ClientMessage}";
                    case ValidationIssueCode.PackageIsZip64:
                        return "Zip64 packages are not supported.";
                    case ValidationIssueCode.OnlyAuthorSignaturesSupported:
                        return "Signed packages must only have an author signature. Other signature types are not supported.";
                    case ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported:
                        return "Author countersignatures and repository countersignatures are not supported.";
                    case ValidationIssueCode.OnlySignatureFormatVersion1Supported:
                        return "**NU3007:** Package signatures must have format version 1.";
                    case ValidationIssueCode.AuthorCounterSignaturesNotSupported:
                        return "Author countersignatures are not supported.";
                    case ValidationIssueCode.PackageIsNotSigned:
                        return "This package must be signed with a registered certificate. [Read more...](https://aka.ms/nuget-signed-ref)";
                    case ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate:
                        var certIssue = (UnauthorizedCertificateFailure)validationIssue;
                        return $"The package was signed, but the signing certificate (SHA-1 thumbprint {certIssue.Sha1Thumbprint}) is not associated with your account. You must register this certificate to publish signed packages. [Read more...](https://aka.ms/nuget-signed-ref)";
                    case ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch:
                        return "The checksum does not match for the dll(s) and corresponding pdb(s).";
                    case ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound:
                        return "The uploaded symbols package contains pdb(s) for a corresponding dll(s) not found in the nuget package.";
                    case ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable:
                        return "The uploaded symbols package contains one or more pdbs that are not portable.";
                    case ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols:
                        return "The uploaded symbols package does not contain any symbol files.";
                    case ValidationIssueCode.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction:
                        return "The uploaded symbols package contains entries that are not safe for extraction.";
                    default:
                        return "There was an unknown failure when validating your package.";
                }
            }
        }

        public class TheSymbolPackageAddedMessage
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public async Task WillSendEmailToAllOwners(string version)
            {
                // Arrange
                var nugetVersion = new NuGetVersion(version);
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = true }
                    }
                };
                var package = new Package
                {
                    Version = version,
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);
                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 12
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                var emailSettingsUrl = "https://localhost/account";
                var symbolPackageAddedMessage = new SymbolPackageAddedMessage(
                    configurationService.Current,
                    symbolPackage,
                    packageUrl,
                    supportUrl,
                    emailSettingsUrl,
                    warningMessages: null);

                // Act
                await messageService.SendMessageAsync(symbolPackageAddedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Symbol package published - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The symbol package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was recently published on {TestGalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({supportUrl}).", message.Body);
            }

            [Fact]
            public async Task WillNotSendEmailToOwnerThatOptsOut()
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", NotifyPackagePushed = true },
                        new User { EmailAddress = "flynt@example.com", NotifyPackagePushed = false }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);
                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 12
                };
                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var symbolPackageAddedMessage = new SymbolPackageAddedMessage(
                    configurationService.Current,
                    symbolPackage,
                    "http://dummy1",
                    "http://dummy2",
                    "http://dummy3",
                    warningMessages: null);

                // Act
                await messageService.SendMessageAsync(symbolPackageAddedMessage);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Single(message.To);
            }

            [Fact]
            public async Task WillNotAttemptToSendIfNoOwnersAllow()
            {
                // Arrange
                var packageRegistration = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = false },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    }
                };
                var package = new Package
                {
                    Version = "1.2.3",
                    PackageRegistration = packageRegistration,
                    User = new User("userThatPushed")
                };
                packageRegistration.Packages.Add(package);
                var symbolPackage = new SymbolPackage()
                {
                    Package = package,
                    Key = 12
                };

                var configurationService = GetConfigurationService();
                var messageService = TestableMarkdownMessageService.Create(configurationService);
                var symbolPackageAddedMessage = new SymbolPackageAddedMessage(
                    configurationService.Current,
                    symbolPackage,
                    "http://dummy1",
                    "http://dummy2",
                    "http://dummy3",
                    warningMessages: null);

                // Act
                await messageService.SendMessageAsync(symbolPackageAddedMessage);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }
               
        private static void AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(MailMessage message, Organization organization)
        {
            AssertMessageSentToMembersOfOrganizationWithPermissionOnly(message, organization, ActionsRequiringPermissions.HandlePackageOwnershipRequest);
        }

        private static void AssertMessageSentToAccountManagersOfOrganizationOnly(MailMessage message, Organization organization)
        {
            AssertMessageSentToMembersOfOrganizationWithPermissionOnly(message, organization, ActionsRequiringPermissions.ManageAccount);
        }

        private static Organization GetOrganizationWithRecipients()
        {
            var org = GetOrganizationWithoutRecipients();

            var admin1 = new User("admin1") { Key = 1, EmailAddress = "admin1@org.com", EmailAllowed = true };
            var admin2 = new User("admin2") { Key = 2, EmailAddress = "admin2@org.com", EmailAllowed = true };

            org.Members.Add(GetMembershipForPackageOwnership(org, admin1, true));
            org.Members.Add(GetMembershipForPackageOwnership(org, admin2, true));

            return org;
        }

        private static Organization GetOrganizationWithoutRecipients()
        {
            var collaborator1 = new User("collaborator") { Key = 3, EmailAddress = "collab@org.com", EmailAllowed = true };
            var collaborator2 = new User("collaboratorUnallowed") { Key = 4, EmailAddress = "collabUnallowed@org.com", EmailAllowed = false };
            var admin3 = new User("adminUnallowed") { Key = 5, EmailAddress = "adminUnallowed@org.com", EmailAllowed = false };
            var org = new Organization("org")
            {
                Key = 6,
                EmailAddress = "org@org.com",
                Members = new List<Membership>()
            };

            org.Members.Add(GetMembershipForPackageOwnership(org, collaborator1, false));
            org.Members.Add(GetMembershipForPackageOwnership(org, collaborator2, false));
            org.Members.Add(GetMembershipForPackageOwnership(org, admin3, true));

            return org;
        }

        private static Membership GetMembershipForPackageOwnership(Organization org, User member, bool isAdmin)
        {
            return new Membership
            {
                IsAdmin = isAdmin,
                Member = member,
                Organization = org
            };
        }

        private static void AssertMessageSentToMembersOfOrganizationWithPermissionOnly(MailMessage message, Organization organization, ActionRequiringAccountPermissions action)
        {
            var membersAllowedToAct = organization.Members
                .Where(m => action.CheckPermissions(m.Member, m.Organization) == PermissionsCheckResult.Allowed)
                .Select(m => m.Member);

            // Each member must appear in the To list at least once.
            foreach (var member in membersAllowedToAct)
            {
                Assert.True(!member.EmailAllowed || message.To.Any(a => member.EmailAddress == a.Address));
            }

            // The size of the To list and admins should be the same.
            Assert.Equal(membersAllowedToAct.Count(m => m.EmailAllowed), message.To.Count());
        }
    }
}