// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class MessageServiceFacts
    {
        public static readonly MailAddress TestGalleryOwner = new MailAddress("joe@example.com", "Joe Shmoe");
        public static readonly MailAddress TestGalleryNoReplyAddress = new MailAddress("noreply@example.com", "No Reply");

        public class TheReportAbuseMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailToGalleryOwner()
            {
                // Arrange
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1"
                };
                
                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.ReportAbuse(
                    new ReportPackageRequest
                    {
                        AlreadyContactedOwners = true,
                        FromAddress = from,
                        Message = "Abuse!",
                        Package = package,
                        Reason = "Reason!",
                        RequestingUser = null,
                        Signature = "Joe Schmoe",
                        Url = TestUtility.MockUrlHelper(),
                    });
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
            public void WillCopySenderIfAsked()
            {
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1",
                };
                
                var messageService = TestableMessageService.Create(GetConfigurationService());

                var reportPackageRequest = new ReportPackageRequest
                {
                    AlreadyContactedOwners = true,
                    FromAddress = from,
                    Message = "Abuse!",
                    Package = package,
                    Reason = "Reason!",
                    RequestingUser = new User
                    {
                        Username = "Joe Schmoe",
                        EmailAddress = "joe@example.com"
                    },
                    Signature = "Joe Schmoe",
                    Url = TestUtility.MockUrlHelper(),
                    CopySender = true,
                };
                messageService.ReportAbuse(reportPackageRequest);

                var message = messageService.MockMailSender.Sent.Single();
                Assert.Equal(TestGalleryOwner, message.To.Single());
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(reportPackageRequest.FromAddress, message.ReplyToList.Single());
                Assert.Equal(reportPackageRequest.FromAddress, message.CC.Single());
                Assert.DoesNotContain("Owners", message.Body);
            }
        }

        public class TheReportMyPackageMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailToGalleryOwner()
            {
                var from = new MailAddress("legit@example.com", "too");
                var owner = new User
                {
                    Username = "too",
                    EmailAddress = "legit@example.com",
                };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "smangit",
                        Owners = new Collection<User> { owner }
                    },
                    Version = "1.42.0.1"
                };
                
                var messageService = TestableMessageService.Create(GetConfigurationService());

                messageService.ReportMyPackage(
                    new ReportPackageRequest
                    {
                        FromAddress = from,
                        Message = "Abuse!",
                        Package = package,
                        Reason = "Reason!",
                        RequestingUser = owner,
                        Signature = "Joe Schmoe",
                        Url = TestUtility.MockUrlHelper(),
                    });

                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(TestGalleryOwner, message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("legit@example.com", message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Owner Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
                Assert.Contains("Reason!", message.Body);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("too (legit@example.com)", message.Body);
                Assert.Contains("smangit", message.Body);
                Assert.Contains("1.42.0.1", message.Body);
            }

            [Fact]
            public void WillCopySenderIfAsked()
            {
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1",
                };
                
                var messageService = TestableMessageService.Create(GetConfigurationService());

                var reportPackageRequest = new ReportPackageRequest
                {
                    AlreadyContactedOwners = true,
                    FromAddress = from,
                    Message = "Abuse!",
                    Package = package,
                    Reason = "Reason!",
                    RequestingUser = new User
                    {
                        Username = "Joe Schmoe",
                        EmailAddress = "joe@example.com"
                    },
                    Signature = "Joe Schmoe",
                    Url = TestUtility.MockUrlHelper(),
                    CopySender = true,
                };
                messageService.ReportMyPackage(reportPackageRequest);

                var message = messageService.MockMailSender.Sent.Single();
                Assert.Equal(TestGalleryOwner, message.To.Single());
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(reportPackageRequest.FromAddress, message.ReplyToList.Single());
                Assert.Equal(reportPackageRequest.FromAddress, message.CC.Single());
                Assert.DoesNotContain("Owners", message.Body);
            }
        }

        public class TheSendContactOwnersMessageMethod
            : TestContainer
        {
            [Fact]
            public void WillCopySenderIfAsked()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // act
                messageService.SendContactOwnersMessage(from, package, "http://someurl/", "Test message", "http://someotherurl/", true);
                var messages = messageService.MockMailSender.Sent;

                // assert
                Assert.Equal(2, messages.Count);
                Assert.Equal(package.PackageRegistration.Owners.Count, messages[0].To.Count);
                Assert.Equal(1, messages[1].To.Count);
                Assert.Equal(ownerAddress, messages[0].To[0].Address);
                Assert.Equal(ownerAddress2, messages[0].To[1].Address);
                Assert.Equal(messages[1].ReplyToList.Single(), messages[1].To.First());
                Assert.Equal(TestGalleryOwner, messages[0].From);
                Assert.Equal(TestGalleryOwner, messages[1].From);
                Assert.Equal(fromAddress, messages[0].ReplyToList.Single().Address);
                Assert.Equal(fromAddress, messages[1].ReplyToList.Single().Address);
            }

            [Fact]
            public void WillSendEmailToAllOwners()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());

                var packageUrl = "http://packageUrl/";
                messageService.SendContactOwnersMessage(from, package, packageUrl, "Test message", "http://emailSettingsUrl/", false);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(owner1Email, message.To[0].Address);
                Assert.Equal(owner2Email, message.To[1].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(userEmail, message.ReplyToList.Single().Address);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Message for owners of the package '{id}'", message.Subject);
                Assert.Contains("Test message", message.Body);
                Assert.Contains(
                    $"User {userUsername} &lt;{userEmail}&gt; sends the following message to the owners of Package '[{id} {version}]({packageUrl})'.", 
                    message.Body);
            }

            [Fact]
            public void WillNotSendEmailToOwnerThatOptsOut()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());

                messageService.SendContactOwnersMessage(from, package, "http://someurl/", "Test message", "http://someotherurl/", false);
                var message = messageService.MockMailSender.Sent.Last();

                // assert
                Assert.Equal(ownerAddress, message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendContactOwnersMessage(from, package, "http://someurl/", "Test message", "http://someotherurl/", false);

                // assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }

            [Fact]
            public void WillNotCopySenderIfNoOwnersAllow()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendContactOwnersMessage(from, package, "http://someurl/", "Test message", "http://someotherurl/", false);

                // assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendNewAccountEmailMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmailToNewUser(bool isOrganization)
            {
                var unconfirmedEmailAddress = "unconfirmed@unconfirmed.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.UnconfirmedEmailAddress = unconfirmedEmailAddress;

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendNewAccountEmail(user, "http://example.com/confirmation-token-url");
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(unconfirmedEmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please verify your account", message.Subject);
                Assert.Contains($"Thank you for {(isOrganization ? $"creating an organization on the" : $"registering with the")} {TestGalleryOwner.DisplayName}.", message.Body);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }

        public class TheSendEmailChangeConfirmationNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmail(bool isOrganization)
            {
                var unconfirmedEmailAddress = "unconfirmed@unconfirmed.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.UnconfirmedEmailAddress = unconfirmedEmailAddress;
                var tokenUrl = "http://example.com/confirmation-token-url";

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendEmailChangeConfirmationNotice(user, tokenUrl);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.UnconfirmedEmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please verify your {(isOrganization ? "organization's" : "account's")} new email address", message.Subject);
                Assert.Contains(tokenUrl, message.Body);
            }
        }

        public class TheSendEmailChangeNoticeToPreviousEmailAddressMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmail(bool isOrganization)
            {
                var newEmail = "new@email.com";
                var user = isOrganization ? new Organization("organization") : new User("user");
                user.EmailAddress = newEmail;
                var oldEmail = "old@email.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendEmailChangeNoticeToPreviousEmailAddress(user, oldEmail);
                var message = messageService.MockMailSender.Sent.Last();

                var accountString = isOrganization ? "organization" : "account";

                Assert.Equal(oldEmail, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Recent changes to your {accountString}'s email", message.Subject);
                Assert.Contains($"The email address associated with your {TestGalleryOwner.DisplayName} {accountString} was recently changed from _{oldEmail}_ to _{user.EmailAddress}_.", message.Body);
            }
        }

        public class TheSendPackageOwnerRequestMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SendsPackageOwnerRequestConfirmationUrl(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequest(from, to, package, packageUrl, confirmationUrl, rejectionUrl, userMessage, string.Empty);
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
            public void SendsPackageOwnerRequestConfirmationUrlWithoutUserMessage(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequest(from, to, package, packageUrl, confirmationUrl, rejectionUrl, string.Empty, string.Empty);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.DoesNotContain("The user 'Existing' added the following message for you", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DoesNotSendRequestIfUserDoesNotAllowEmails(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequest(from, to, package, packageUrl, confirmationUrl, rejectionUrl, string.Empty, string.Empty);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerRequestInitiatedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void SendsNotice()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestInitiatedNotice(requestingOwner, receivingOwner, newOwner, package, cancelUrl);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(receivingOwner.EmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(newOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Package ownership request for '{package.Id}'", message.Subject);
                Assert.Contains($"The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.", message.Body);
                Assert.Contains($"[{cancelUrl}]({cancelUrl})", message.Body);
            }

            [Fact]
            public void DoesNotSendNoticeIfUserDoesNotAllowEmails()
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestInitiatedNotice(requestingOwner, receivingOwner, newOwner, package, cancelUrl);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerRequestRejectionNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SendsNotice(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestRejectionNotice(requestingOwner, newOwner, package);
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
            public void DoesNotSendNoticeIfUserDoesNotAllowEmails(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestRejectionNotice(requestingOwner, newOwner, package);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerRequestCancellationNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SendsNotice(bool isOrganization)
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var newOwner = isOrganization ? GetOrganizationWithRecipients() : new User();
                newOwner.Username = "Noob";
                newOwner.EmailAddress = "new-owner@example.com";
                newOwner.EmailAllowed = true;
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);
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
            public void DoesNotSendNoticeIfUserDoesNotAllowEmails(bool isOrganization)
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

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerAddedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SendsPackageOwnerAddedNotice(bool isOrganization)
            {
                // Arrange
                var toUser = isOrganization ? GetOrganizationWithRecipients() : new User();
                toUser.Username = "Existing";
                toUser.EmailAddress = "existing-owner@example.com";
                toUser.EmailAllowed = true;
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = "packageUrl";

                // Act
                messageService.SendPackageOwnerAddedNotice(toUser, newUser, package, packageUrl);

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
                Assert.Equal(TestGalleryNoReplyAddress.Address, "noreply@example.com");
                Assert.Contains($"Package ownership update for '{package.Id}'", message.Subject);
                Assert.Contains($"User '{newUser.Username}' is now an owner of the package ['{package.Id}']({packageUrl}).", message.Body);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void DoesNotSendPackageOwnerAddedNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                // Arrange
                var toUser = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                toUser.Username = "Existing";
                toUser.EmailAddress = "existing-owner@example.com";
                toUser.EmailAllowed = false;
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = false };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendPackageOwnerAddedNotice(toUser, newUser, package, "packageUrl");

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerRemovedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void SendsPackageOwnerRemovedNotice(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "old-owner@example.com";
                to.EmailAllowed = true;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRemovedNotice(from, to, package);
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
            public void DoesNotSendRemovedNoticeIfUserDoesNotAllowEmails(bool isOrganization)
            {
                var to = isOrganization ? GetOrganizationWithoutRecipients() : new User();
                to.Username = "Noob";
                to.EmailAddress = "old-owner@example.com";
                to.EmailAllowed = false;
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRemovedNotice(from, to, package);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        private static void AssertMessageSentToPackageOwnershipManagersOfOrganizationOnly(MailMessage message, Organization organization)
        {
            AssertMessageSentToMembersOfOrganizationWithPermissionOnly(message, organization, ActionsRequiringPermissions.HandlePackageOwnershipRequest);
        }

        public class TheSendResetPasswordInstructionsMethod
            : TestContainer
        {
            [Fact]
            public void WillSendInstructions()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "too" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPasswordResetInstructions(user, "http://example.com/pwd-reset-token-url", true);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Please reset your password.", message.Subject);
                Assert.Contains("Click the following link within the next", message.Body);
                Assert.Contains("http://example.com/pwd-reset-token-url", message.Body);
            }
        }

        public class TheSendCredentialRemovedNoticeMethod
            : TestContainer
        {
            private AuthenticationService _authenticationService;

            public TheSendCredentialRemovedNoticeMethod()
            {
                _authenticationService = GetService<AuthenticationService>();
            }

            [Fact]
            public void UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                const string MicrosoftAccountCredentialName = "Microsoft account";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                messageService.SendCredentialRemovedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, MicrosoftAccountCredentialName), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialRemoved_Body, MicrosoftAccountCredentialName), message.Body);
            }

            [Fact]
            public void UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreatePasswordCredential("bogus");

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendCredentialRemovedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_Password), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialRemoved_Body, Strings.CredentialType_Password), message.Body);
            }

            [Fact]
            public void ApiKeyRemovedMessageIsCorrect()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1)).WithDefaultScopes();
                cred.Description = "new api key";
                cred.User = user;

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendCredentialRemovedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialRemoved_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_ApiKey), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_ApiKeyRemoved_Body, cred.Description), message.Body);
            }
        }

        public class TheSendCredentialAddedNoticeMethod
            : TestContainer
        {
            private AuthenticationService _authenticationService;

            public TheSendCredentialAddedNoticeMethod()
            {
                _authenticationService = GetService<AuthenticationService>();
            }

            [Fact]
            public void UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                const string MicrosoftAccountCredentialName = "Microsoft account";

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendCredentialAddedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, MicrosoftAccountCredentialName), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialAdded_Body, MicrosoftAccountCredentialName), message.Body);
            }

            [Fact]
            public void UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = new CredentialBuilder().CreatePasswordCredential("bogus");

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendCredentialAddedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_Password), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_CredentialAdded_Body, Strings.CredentialType_Password), message.Body);
            }

            [Fact]
            public void ApiKeyAddedMessageIsCorrect()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = TestCredentialHelper.CreateV2ApiKey(Guid.NewGuid(), TimeSpan.FromDays(1)).WithDefaultScopes();
                cred.Description = "new api key";
                cred.User = user;

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendCredentialAddedNotice(user, _authenticationService.DescribeCredential(cred));
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal(string.Format(Strings.Emails_CredentialAdded_Subject, TestGalleryOwner.DisplayName, Strings.CredentialType_ApiKey), message.Subject);
                Assert.Contains(string.Format(Strings.Emails_ApiKeyAdded_Body, cred.Description), message.Body);
            }
        }

        public class TheSendPackageAddedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public void WillSendEmailToAllOwners(string version)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                var emailSettingsUrl = "https://localhost/account";
                messageService.SendPackageAddedNotice(package, packageUrl, supportUrl, emailSettingsUrl);

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
            public void WillNotSendEmailToOwnerThatOptsOut()
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageAddedNotice(package, "http://dummy1", "http://dummy2", "http://dummy3");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow()
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageAddedNotice(package, "http://dummy1", "http://dummy2", "http://dummy3");

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageValidationFailedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public void WillSendEmailToAllOwners(string version)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                messageService.SendPackageValidationFailedNotice(package, packageUrl, supportUrl);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package validation failed - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) failed validation and was therefore not published on {TestGalleryOwner.DisplayName}. " +
                    $"Note that the package will not be available for consumption and you will not be able to push the same package ID and version until further action is taken. " +
                    $"Please [contact support]({supportUrl}) for next steps.", message.Body);
            }

            public static IEnumerable<object[]> EmailSettingsCombinations
                => from u1pa in new[] { true, false }
                   from u2pa in new[] { true, false }
                   from u1ea in new[] { true, false }
                   from u2ea in new[] { true, false }
                   select new object[] { u1pa, u2pa, u1ea, u2ea };

            [Theory]
            [MemberData(nameof(EmailSettingsCombinations))]
            public void WillSendEmailToOwnersRegardlessOfSettings(bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageValidationFailedNotice(package, "http://dummy1", "http://dummy2");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(2, message.To.Count);
            }
        }

        public class TheSendSignedPackageNotAllowedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public void WillSendEmailToAllOwners(string version)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                var announcementsUrl = "https://example.com/announcements";
                var twitterUrl = "https://example.com/twitter";
                messageService.SendSignedPackageNotAllowedNotice(package, packageUrl, announcementsUrl, twitterUrl);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package validation failed - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) could not be published since it is signed. " +
                    $"{TestGalleryOwner.DisplayName} does not accept signed packages at this moment. To be notified when {TestGalleryOwner.DisplayName} starts accepting signed packages, " +
                    $"and more, watch our [Announcements]({announcementsUrl}) page or follow us on [Twitter]({twitterUrl}).", message.Body);
            }

            public static IEnumerable<object[]> EmailSettingsCombinations
                => from u1pa in new[] { true, false }
                   from u2pa in new[] { true, false }
                   from u1ea in new[] { true, false }
                   from u2ea in new[] { true, false }
                   select new object[] { u1pa, u2pa, u1ea, u2ea };

            [Theory]
            [MemberData(nameof(EmailSettingsCombinations))]
            public void WillSendEmailToOwnersRegardlessOfSettings(bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendSignedPackageNotAllowedNotice(package, "http://dummy1", "http://dummy2", "http://dummy3");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(2, message.To.Count);
            }
        }

        public class TheSendValidationTakingTooLongNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public void WillSendEmailToAllOwners(string version)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                messageService.SendValidationTakingTooLongNotice(package, packageUrl);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package validation taking longer than expected - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"It is taking longer than expected for your package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) to get published.\n\n" +
                    $"We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.\n\n" +
                    $"Thank you for your patience.", message.Body);
            }

            public static IEnumerable<object[]> EmailSettingsCombinations
                => from u1pa in new[] { true, false }
                   from u2pa in new[] { true, false }
                   from u1ea in new[] { true, false }
                   from u2ea in new[] { true, false }
                   select new object[] { u1pa, u2pa, u1ea, u2ea };

            [Theory]
            [MemberData(nameof(EmailSettingsCombinations))]
            public void WillSendEmailToOwnersRegardlessOfSettings(bool user1PushAllowed, bool user2PushAllowed, bool user1EmailAllowed, bool user2EmailAllowed)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendValidationTakingTooLongNotice(package, "http://dummy1");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(2, message.To.Count);
            }
        }

        public class TheSendPackageUploadedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData("1.2.3")]
            [InlineData("1.2.3-alpha")]
            [InlineData("1.2.3-alpha.1")]
            [InlineData("1.2.3+metadata")]
            [InlineData("1.2.3-alpha+metadata")]
            [InlineData("1.2.3-alpha.1+metadata")]
            public void WillSendEmailToAllOwners(string version)
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";
                var emailSettingsUrl = "https://localhost/account";
                messageService.SendPackageUploadedNotice(package, packageUrl, supportUrl, emailSettingsUrl);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Contains($"[{TestGalleryOwner.DisplayName}] Package uploaded - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.DoesNotContain("publish", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was recently uploaded to {TestGalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({supportUrl}).", message.Body);
            }

            [Fact]
            public void WillNotSendEmailToOwnerThatOptsOut()
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageUploadedNotice(package, "http://dummy1", "http://dummy2", "http://dummy3");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow()
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

                // Act
                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageUploadedNotice(package, "http://dummy1", "http://dummy2", "http://dummy3");

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageDeletedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailToAllOwners()
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
                
                var messageService = TestableMessageService.Create(GetConfigurationService());
                var packageUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}";
                var supportUrl = $"https://localhost/packages/{packageRegistration.Id}/{nugetVersion.ToNormalizedString()}/ReportMyPackage";

                // Act
                messageService.SendPackageDeletedNotice(package, packageUrl, supportUrl);

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
        public class TheSendOrganizationTransformRequestMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequest(accountToTransform, adminUser, profileUrl, confirmationUrl, rejectionUrl);

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
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequest(accountToTransform, adminUser, profileUrl, confirmationUrl, rejectionUrl);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationTransformInitiatedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformInitiatedNotice(accountToTransform, adminUser, cancelUrl);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Organization transformation for account '{accountToTransform.Username}'", message.Subject);
                Assert.Contains($"We have received a request to transform account '{accountToTransform.Username}' into an organization with user '{adminUser.Username}' as its admin.", message.Body);
                Assert.Contains($"[{cancelUrl}]({cancelUrl})", message.Body);
                Assert.Contains($"If you did not request this change, please contact support by responding to this email.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformInitiatedNotice(accountToTransform, adminUser, cancelUrl);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationTransformRequestAcceptedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestAcceptedNotice(accountToTransform, adminUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Account '{accountToTransform.Username}' has been transformed into an organization", message.Subject);
                Assert.Contains($"Account '{accountToTransform.Username}' has been transformed into an organization with user '{adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestAcceptedNotice(accountToTransform, adminUser);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationTransformRequestRejectedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = true };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestRejectedNotice(accountToTransform, adminUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(accountToTransform.EmailAddress, message.To[0].Address);
                Assert.Equal(adminUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Transformation of account '{accountToTransform.Username}' has been cancelled", message.Subject);
                Assert.Contains($"Transformation of account '{accountToTransform.Username}' has been cancelled by user '{adminUser.Username}'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com", EmailAllowed = false };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestRejectedNotice(accountToTransform, adminUser);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationTransformRequestCancelledNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestCancelledNotice(accountToTransform, adminUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(adminUser.EmailAddress, message.To[0].Address);
                Assert.Equal(accountToTransform.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Transformation of account '{accountToTransform.Username}' has been cancelled", message.Subject);
                Assert.Contains($"Transformation of account '{accountToTransform.Username}' has been cancelled by user '{accountToTransform.Username}'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var adminUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationTransformRequestCancelledNotice(accountToTransform, adminUser);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMembershipRequestMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var adminUser = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var newUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequest(organization, newUser, adminUser, isAdmin, profileUrl, confirmationUrl, rejectionUrl);

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
            public void WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var adminUser = new User("bumblebee") { EmailAddress = "bumblebee@transformers.com" };
                var newUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };
                var profileUrl = "www.profile.com";
                var confirmationUrl = "www.confirm.com";
                var rejectionUrl = "www.rejection.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequest(organization, newUser, adminUser, isAdmin, profileUrl, confirmationUrl, rejectionUrl);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMembershipRequestInitiatedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = GetOrganizationWithRecipients();
                var requestingUser = new User("optimusprime") { EmailAddress = "optimusprime@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestInitiatedNotice(organization, requestingUser, pendingUser, isAdmin, cancelUrl);

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
            public void WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var organization = GetOrganizationWithoutRecipients();
                var requestingUser = new User("optimusprime") { EmailAddress = "optimusprime@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var cancelUrl = "www.cancel.com";

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestInitiatedNotice(organization, requestingUser, pendingUser, isAdmin, cancelUrl);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMembershipRequestRejectedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = GetOrganizationWithRecipients();
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestRejectedNotice(organization, pendingUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                AssertMessageSentToAccountManagersOfOrganizationOnly(message, organization);

                Assert.Equal(pendingUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' declined", message.Subject);
                Assert.Contains($"The user '{pendingUser.Username}' has declined your request to become a member of your organization.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var organization = GetOrganizationWithoutRecipients();
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestRejectedNotice(organization, pendingUser);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMembershipRequestCancelledNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = true };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestCancelledNotice(organization, pendingUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(pendingUser.EmailAddress, message.To[0].Address);
                Assert.Equal(organization.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' cancelled", message.Subject);
                Assert.Contains($"The request for you to become a member of '{organization.Username}' has been cancelled.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var accountToTransform = new Organization("transformers") { EmailAddress = "transformers@transformers.com" };
                var pendingUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com", EmailAllowed = false };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMembershipRequestCancelledNotice(accountToTransform, pendingUser);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMemberUpdatedNoticeMethod
            : TestContainer
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void WillSendEmailIfEmailAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = true };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var membership = new Membership { Organization = organization, Member = member, IsAdmin = isAdmin };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMemberUpdatedNotice(organization, membership);

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
            public void WillNotSendEmailIfEmailNotAllowed(bool isAdmin)
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = false };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };
                var membership = new Membership { Organization = organization, Member = member, IsAdmin = isAdmin };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMemberUpdatedNotice(organization, membership);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendOrganizationMemberRemovedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void WillSendEmailIfEmailAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = true };
                var removedUser = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMemberRemovedNotice(organization, removedUser);

                // Assert
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(organization.EmailAddress, message.To[0].Address);
                Assert.Equal(removedUser.EmailAddress, message.ReplyToList[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress, message.From);
                Assert.Equal($"[{TestGalleryOwner.DisplayName}] Membership update for organization '{organization.Username}'", message.Subject);
                Assert.Contains($"The user '{removedUser.Username}' is no longer a member of organization '{organization.Username}'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailIfEmailNotAllowed()
            {
                // Arrange
                var organization = new Organization("transformers") { EmailAddress = "transformers@transformers.com", EmailAllowed = false };
                var member = new User("shia_labeouf") { EmailAddress = "justdoit@shia.com" };

                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendOrganizationMemberRemovedNotice(organization, member);

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
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

        private static void AddMembershipForPackageOwnership(Organization org, User member, bool isAdmin)
        {
            var membership = GetMembershipForPackageOwnership(org, member, isAdmin);

            org.Members.Add(membership);
            member.Organizations = new[] { membership };
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

        public class TestableMessageService 
            : MessageService
        {
            private TestableMessageService(IGalleryConfigurationService configurationService)
            {
                configurationService.Current.GalleryOwner = TestGalleryOwner;
                configurationService.Current.GalleryNoReplyAddress = TestGalleryNoReplyAddress;

                Config = configurationService.Current;
                MailSender = MockMailSender = new TestMailSender();
            }

            public Mock<AuthenticationService> MockAuthService { get; protected set; }
            public TestMailSender MockMailSender { get; protected set; }

            public static TestableMessageService Create(IGalleryConfigurationService configurationService)
            {
                return new TestableMessageService(configurationService);
            }
        }

        // Normally I don't like hand-written mocks, but this actually seems appropriate - anurse
        public class TestMailSender : IMailSender
        {
            public IList<MailMessage> Sent { get; private set; }

            public TestMailSender()
            {
                Sent = new List<MailMessage>();
            }

            public void Send(MailMessage mailMessage)
            {
                Sent.Add(mailMessage);
            }

            public void Send(MailAddress fromAddress, MailAddress toAddress, string subject, string markdownBody)
            {
                Send(new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = markdownBody
                });
            }

            public void Send(string fromAddress, string toAddress, string subject, string markdownBody)
            {
                Send(new MailMessage(fromAddress, toAddress, subject, markdownBody));
            }
        }
    }
}