// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;
using Moq;
using NuGetGallery.Areas.Admin.DynamicData;
using Xunit;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery
{
    public class MessageServiceFacts
    {
        public static readonly MailAddress TestGalleryOwner = new MailAddress("joe@example.com", "Joe Shmoe");

        public class TheReportAbuseMethod
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

                var messageService = new TestableMessageService();

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

                var messageService = new TestableMessageService();

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

        public class TheReportMyPackageMethod : TestContainer
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
                var messageService = new TestableMessageService();

                messageService.ReportMyPackage(
                    new ReportPackageRequest
                    {
                        FromAddress = from,
                        Message = "Abuse!",
                        Package = package,
                        Reason = "Reason!",
                        RequestingUser = owner,
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
                var messageService = new TestableMessageService();

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
        {
            [Fact]
            public void WillCopySenderIfAsked()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = true },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = true }
                    };
                var messageService = new TestableMessageService();

                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/", true);

                var messages = messageService.MockMailSender.Sent;
                Assert.Equal(2, messages.Count);
                Assert.Equal(package.Owners.Count, messages[0].To.Count);
                Assert.Equal(1, messages[1].To.Count);
                Assert.Equal("yung@example.com", messages[0].To[0].Address);
                Assert.Equal("flynt@example.com", messages[0].To[1].Address);
                Assert.Equal(messages[1].ReplyToList.Single(), messages[1].To.First());
                Assert.Equal(TestGalleryOwner, messages[0].From);
                Assert.Equal(TestGalleryOwner, messages[1].From);
                Assert.Equal("smangit@example.com", messages[0].ReplyToList.Single().Address);
                Assert.Equal("smangit@example.com", messages[1].ReplyToList.Single().Address);
            }

            [Fact]
            public void WillSendEmailToAllOwners()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = true },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = true }
                    }
                };

                var messageService = new TestableMessageService();
                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/", false);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("smangit@example.com", message.ReplyToList.Single().Address);
                Assert.Contains("[Joe Shmoe] Message for owners of the package 'smangit'", message.Subject);
                Assert.Contains("Test message", message.Body);
                Assert.Contains(
                    "User flossy &lt;smangit@example.com&gt; sends the following message to the owners of Package 'smangit'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailToOwnerThatOptsOut()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = true },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    }
                };

                var messageService = new TestableMessageService();
                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/", false);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration
                {
                    Id = "smangit",
                    Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = false },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    }
                };

                var messageService = new TestableMessageService();
                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/", false);
                
                Assert.Empty(messageService.MockMailSender.Sent);
            }

            [Fact]
            public void WillNotCopySenderIfNoOwnersAllow()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = false },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    };

                var messageService = new TestableMessageService();
                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/", false);
                
                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendNewAccountEmailMethod
        {
            [Fact]
            public void WillSendEmailToNewUser()
            {
                var to = new MailAddress("legit@example.com", "too");
                
                var messageService = new TestableMessageService();
                messageService.SendNewAccountEmail(to, "http://example.com/confirmation-token-url");
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("[Joe Shmoe] Please verify your account.", message.Subject);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }

        public class TheSendPackageOwnerRequestMethod
        {
            [Fact]
            public void SendsPackageOwnerRequestConfirmationUrl()
            {
                var to = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = true };
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string confirmationUrl = "http://example.com/confirmation-token-url";

                var messageService = new TestableMessageService();
                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("new-owner@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("existing-owner@example.com", message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] The user 'Existing' wants to add you as an owner of the package 'CoolStuff'.", message.Subject);
                Assert.Contains(confirmationUrl, message.Body);
                Assert.Contains("The user 'Existing' wants to add you as an owner of the package 'CoolStuff'.", message.Body);
            }

            [Fact]
            public void DoesNotSendRequestIfUserDoesNotAllowEmails()
            {
                var to = new User { Username = "Noob", EmailAddress = "new-owner@example.com", EmailAllowed = false };
                var from = new User { Username = "Existing", EmailAddress = "existing-owner@example.com" };
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string confirmationUrl = "http://example.com/confirmation-token-url";

                var messageService = new TestableMessageService();
                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);

                Assert.Empty(messageService.MockMailSender.Sent);
            }
        }

        public class TheSendResetPasswordInstructionsMethod
        {
            [Fact]
            public void WillSendInstructions()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "too" };

                var messageService = new TestableMessageService();
                messageService.SendPasswordResetInstructions(user, "http://example.com/pwd-reset-token-url", true);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("[Joe Shmoe] Please reset your password.", message.Subject);
                Assert.Contains("Click the following link within the next", message.Body);
                Assert.Contains("http://example.com/pwd-reset-token-url", message.Body);
            }
        }

        public class TheSendCredentialRemovedNoticeMethod
        {
            [Fact]
            public void UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                var messageService = new TestableMessageService();
                messageService.MockAuthService
                    .Setup(a => a.DescribeCredential(cred))
                    .Returns(new CredentialViewModel() { 
                        AuthUI = new AuthenticatorUI("sign in", "Microsoft Account", "Microsoft Account") 
                    });

                messageService.SendCredentialRemovedNotice(user, cred);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("[Joe Shmoe] Microsoft Account removed from your account", message.Subject);
                Assert.Contains("A Microsoft Account was removed from your account", message.Body);
            }

            [Fact]
            public void UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = CredentialBuilder.CreatePbkdf2Password("bogus");
                var messageService = new TestableMessageService();
                messageService.MockAuthService
                    .Setup(a => a.DescribeCredential(cred))
                    .Returns(new CredentialViewModel() { 
                        TypeCaption = "Password"
                    });

                messageService.SendCredentialRemovedNotice(user, cred);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("[Joe Shmoe] Password removed from your account", message.Subject);
                Assert.Contains("A Password was removed from your account", message.Body);
            }
        }

        public class TheSendCredentialAddedNoticeMethod
        {
            [Fact]
            public void UsesProviderNounToDescribeCredentialIfPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = CredentialBuilder.CreateExternalCredential("MicrosoftAccount", "abc123", "Test User");
                var messageService = new TestableMessageService();
                messageService.MockAuthService
                    .Setup(a => a.DescribeCredential(cred))
                    .Returns(new CredentialViewModel() { 
                        AuthUI = new AuthenticatorUI("sign in", "Microsoft Account", "Microsoft Account") 
                    });

                messageService.SendCredentialAddedNotice(user, cred);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("[Joe Shmoe] Microsoft Account added to your account", message.Subject);
                Assert.Contains("A Microsoft Account was added to your account", message.Body);
            }

            [Fact]
            public void UsesTypeCaptionToDescribeCredentialIfNoProviderNounPresent()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "foo" };
                var cred = CredentialBuilder.CreatePbkdf2Password("bogus");
                var messageService = new TestableMessageService();
                messageService.MockAuthService
                    .Setup(a => a.DescribeCredential(cred))
                    .Returns(new CredentialViewModel() { 
                        TypeCaption = "Password"
                    });

                messageService.SendCredentialAddedNotice(user, cred);
                var message = messageService.MockMailSender.Sent.Last();

                Assert.Equal(user.ToMailAddress(), message.To[0]);
                Assert.Equal(TestGalleryOwner, message.From);
                Assert.Equal("[Joe Shmoe] Password added to your account", message.Subject);
                Assert.Contains("A Password was added to your account", message.Body);
            }
        }

        public class TestableMessageService : MessageService
        {
            public Mock<AuthenticationService> MockAuthService { get; protected set; }
            public Mock<IAppConfiguration> MockConfig { get; protected set; }
            public TestMailSender MockMailSender { get; protected set; }

            public TestableMessageService()
            {
                AuthService = (MockAuthService = new Mock<AuthenticationService>()).Object;
                Config = (MockConfig = new Mock<IAppConfiguration>()).Object;
                MailSender = MockMailSender = new TestMailSender();

                MockConfig.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
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