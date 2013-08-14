using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;
using Moq;
using Xunit;
using NuGetGallery.Configuration;

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
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package
                    {
                        PackageRegistration = new PackageRegistration { Id = "smangit" },
                        Version = "1.42.0.1"
                    };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

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

                Assert.Equal(TestGalleryOwner.Address, message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("legit@example.com", message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
                Assert.Contains("Reason!", message.Body);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("too (legit@example.com)", message.Body);
                Assert.Contains("smangit", message.Body);
                Assert.Contains("1.42.0.1", message.Body);
                Assert.Contains("Yes", message.Body);
            }
        }

        public class TheReportMyPackageMethod
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
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

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

                Assert.Equal(TestGalleryOwner.Address, message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("legit@example.com", message.ReplyToList.Single().Address);
                Assert.Equal("[Joe Shmoe] Owner Support Request for 'smangit' version 1.42.0.1 (Reason: Reason!)", message.Subject);
                Assert.Contains("Reason!", message.Body);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("too (legit@example.com)", message.Body);
                Assert.Contains("smangit", message.Body);
                Assert.Contains("1.42.0.1", message.Body);
            }
        }

        public class TheSendContactOwnersMessageMethod
        {
            [Fact]
            public void WillSendEmailToAllOwners()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = true },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = true }
                    };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/");

                mailSender.Verify(m => m.Send(It.IsAny<MailMessage>()));
                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
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
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = true },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });


                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/");

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[]
                    {
                        new User { EmailAddress = "yung@example.com", EmailAllowed = false },
                        new User { EmailAddress = "flynt@example.com", EmailAllowed = false }
                    };
                var mailSender = new Mock<IMailSender>();
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Throws(new InvalidOperationException());
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/");

                mailSender.Verify(m => m.Send(It.IsAny<MailMessage>()), Times.Never());
                Assert.Null(message);
            }
        }

        public class TheSendNewAccountEmailMethod
        {
            [Fact]
            public void WillSendEmailToNewUser()
            {
                var to = new MailAddress("legit@example.com", "too");
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendNewAccountEmail(to, "http://example.com/confirmation-token-url");

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
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);

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
                var mailSender = new Mock<IMailSender>();
                mailSender.Setup(s => s.Send(It.IsAny<MailMessage>())).Throws(new InvalidOperationException("Should not be called"));
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                var package = new PackageRegistration { Id = "CoolStuff" };
                const string confirmationUrl = "http://example.com/confirmation-token-url";
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);

                Assert.Null(message);
            }
        }

        public class TheSendResetPasswordInstructionsMethod
        {
            [Fact]
            public void WillSendInstructions()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "too" };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IAppConfiguration>();
                config.Setup(x => x.GalleryOwner).Returns(TestGalleryOwner);
                var messageService = new MessageService(mailSender.Object, config.Object);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPasswordResetInstructions(user, "http://example.com/pwd-reset-token-url");

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal(TestGalleryOwner.Address, message.From.Address);
                Assert.Equal("[Joe Shmoe] Please reset your password.", message.Subject);
                Assert.Contains("Click the following link within the next", message.Body);
                Assert.Contains("http://example.com/pwd-reset-token-url", message.Body);
            }
        }
    }
}