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
                Assert.Equal("[Joe Shmoe] Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
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
                Assert.Equal("[Joe Shmoe] Owner Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
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
                Assert.Contains($"[Joe Shmoe] Message for owners of the package '{id}'", message.Subject);
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
            [Fact]
            public void WillSendEmailToNewUser()
            {
                var to = new MailAddress("legit@example.com", "too");

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendNewAccountEmail(to, "http://example.com/confirmation-token-url");
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal("[Joe Shmoe] Please verify your account.", message.Subject);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }

        public class TheSendPackageOwnerRequestMethod
            : TestContainer
        {
            [Fact]
            public void SendsPackageOwnerRequestConfirmationUrl()
            {
                var to = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = true };
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string packageUrl = "http://nuget.local/packages/CoolStuff";
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                const string rejectionUrl = "http://example.com/rejection-token-url";
                const string userMessage = "Hello World!";

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequest(from, to, package, packageUrl, confirmationUrl, rejectionUrl, userMessage, string.Empty);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("new-owner@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal("existing-owner@example.com", message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] The user 'Existing' would like to add you as an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains("The user 'Existing' added the following message for you", message.Body);
                Assert.Contains(userMessage, message.Body);
                Assert.Contains(confirmationUrl, message.Body);
                Assert.Contains(userMessage, message.Body);
                Assert.Contains("The user 'Existing' wants to add you as an owner of the package 'CoolStuff'.", message.Body);
            }

            [Fact]
            public void SendsPackageOwnerRequestConfirmationUrlWithoutUserMessage()
            {
                var to = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = true };
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

            [Fact]
            public void DoesNotSendRequestIfUserDoesNotAllowEmails()
            {
                var to = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = false };
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

        public class TheSendPackageOwnerRequestRejectionNoticeMethod
            : TestContainer
        {
            [Fact]
            public void SendsNotice()
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com", EmailAllowed = true };
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

                Assert.Equal(requestingOwner.EmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(newOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] The user 'Noob' has rejected your request to add them as an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains("The user 'Noob' has rejected your request to add them as an owner of the package 'CoolStuff'.", message.Body);
            }

            [Fact]
            public void DoesNotSendNoticeIfUserDoesNotAllowEmails()
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
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
            [Fact]
            public void SendsNotice()
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = true };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(newOwner.EmailAddress, message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal(requestingOwner.EmailAddress, message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] The user 'Existing' has cancelled their request for you to be added as an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains("The user 'Existing' has cancelled their request for you to be added as an owner of the package 'CoolStuff'.", message.Body);
            }

            [Fact]
            public void DoesNotSendNoticeIfUserDoesNotAllowEmails()
            {
                var requestingOwner = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var newOwner = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
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
            [Fact]
            public void SendsPackageOwnerAddedNotice()
            {
                // Arrange
                var toUser = new User { Username = "Existing", EmailAddress = "existing-owner@example.com", EmailAllowed = true };
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendPackageOwnerAddedNotice(toUser, newUser, package, "packageUrl", "policyMessage");

                // Assert
                var message = messageService.MockMailSender.Sent.Last();
                Assert.Equal("existing-owner@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, "noreply@example.com");
                Assert.Contains("The user 'Noob' is now an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains("This is to inform you that 'Noob' is now an owner of the package", message.Body);
                Assert.Contains("policyMessage", message.Body);
            }

            [Fact]
            public void DoesNotSendPackageOwnerAddedNoticeIfUserDoesNotAllowEmails()
            {
                // Arrange
                var toUser = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var newUser = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = false };
                var package = new PackageRegistration { Id = "CoolStuff" };
                var messageService = TestableMessageService.Create(GetConfigurationService());

                // Act
                messageService.SendPackageOwnerAddedNotice(toUser, newUser, package, "packageUrl", "policyMessage");

                // Assert
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendPackageOwnerRemovedNoticeMethod
            : TestContainer
        {
            [Fact]
            public void SendsPackageOwnerRemovedNotice()
            {
                var to = new User { Username = "Noob", EmailAddress = "old-owner@example.com", EmailAllowed = true };
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRemovedNotice(from, to, package);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("old-owner@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryNoReplyAddress.Address, message.From.Address);
                Assert.Equal("existing-owner@example.com", message.ReplyToList.Single().Address);
                Assert.Contains("The user 'Existing' has removed you as an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains("The user 'Existing' removed you as an owner of the package 'CoolStuff'", message.Body);
            }

            [Fact]
            public void DoesNotSendRemovedNoticeIfUserDoesNotAllowEmails()
            {
                var to = new User { Username = "Noob", EmailAddress = "old-owner@example.com", EmailAllowed = false };
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };

                var messageService = TestableMessageService.Create(GetConfigurationService());
                messageService.SendPackageOwnerRemovedNotice(from, to, package);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
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
                Assert.Equal("[Joe Shmoe] Please reset your password.", message.Subject);
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
                    PackageRegistration = packageRegistration
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
                Assert.Contains($"[Joe Shmoe] Package published - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was just published on Joe Shmoe. If this was not intended, please [contact support]({supportUrl}).", message.Body);
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
                    PackageRegistration = packageRegistration
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
                    PackageRegistration = packageRegistration
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
                Assert.Contains($"[Joe Shmoe] Package validation failed - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) failed validation and was therefore not published on Joe Shmoe. " +
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
                Assert.Contains($"[Joe Shmoe] Package validation failed - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) could not be published since it is signed. " +
                    $"Joe Shmoe does not accept signed packages at this moment. To be notified when Joe Shmoe starts accepting signed packages, " +
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
                    PackageRegistration = packageRegistration
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
                Assert.Contains($"[Joe Shmoe] Package uploaded - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.DoesNotContain("publish", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was just uploaded to Joe Shmoe. If this was not intended, please [contact support]({supportUrl}).", message.Body);
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
                    PackageRegistration = packageRegistration
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
                    PackageRegistration = packageRegistration
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
                Assert.Contains($"[Joe Shmoe] Package deleted - {packageRegistration.Id} {nugetVersion.ToNormalizedString()}", message.Subject);
                Assert.Contains(
                    $"The package [{packageRegistration.Id} {nugetVersion.ToFullString()}]({packageUrl}) was just deleted from Joe Shmoe. If this was not intended, please [contact support]({supportUrl}).", message.Body);
            }
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