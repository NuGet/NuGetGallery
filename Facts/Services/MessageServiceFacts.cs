using System;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class MessageServiceFacts
    {
        public class TheSendContactOwnersMessageMethod
        {
            [Fact]
            public void WillSendEmailToAllOwners()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = true },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = true }
                };
                var mailSender = new Mock<IMailSender>();
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/");

                mailSender.Verify(m => m.Send(It.IsAny<MailMessage>()));
                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Contains("[NuGet Gallery] Message for owners of the package 'smangit'", message.Subject);
                Assert.Contains("Test message", message.Body);
                Assert.Contains("User flossy &lt;smangit@example.com&gt; sends the following message to the owners of Package 'smangit'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailToOwnerThatOptsOut()
            {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = true },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = false }
                };
                var mailSender = new Mock<IMailSender>();
                var setting = new GallerySetting { GalleryOwnerName = "Joe Schmoe", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
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
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = false },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = false }
                };
                var mailSender = new Mock<IMailSender>();
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Throws(new InvalidOperationException());
                var setting = new GallerySetting { GalleryOwnerName = "Joe Schmoe", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendContactOwnersMessage(from, package, "Test message", "http://someurl/");

                mailSender.Verify(m => m.Send(It.IsAny<MailMessage>()), Times.Never());
                Assert.Null(message);
            }
        }

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
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.ReportAbuse(from, package, "Abuse!");

                Assert.Equal("joe@example.com", message.To[0].Address);
                Assert.Equal("[NuGet Gallery] Abuse Report for Package 'smangit' Version '1.42.0.1'", message.Subject);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("User too (legit@example.com) reports the package 'smangit' version '1.42.0.1' as abusive", message.Body);
            }
        }

        public class TheSendNewAccountEmailMethod
        {
            [Fact]
            public void WillSendEmailToNewUser()
            {
                var to = new MailAddress("legit@example.com", "too");
                var mailSender = new Mock<IMailSender>();
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendNewAccountEmail(to, "http://example.com/confirmation-token-url");

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal("[NuGet Gallery] Please verify your account.", message.Subject);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }

        public class TheSendResetPasswordInstructionsMethod
        {
            [Fact]
            public void WillSendInstructions()
            {
                var user = new User { EmailAddress = "legit@example.com", Username = "too" };
                var mailSender = new Mock<IMailSender>();
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPasswordResetInstructions(user, "http://example.com/pwd-reset-token-url");

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal("[NuGet Gallery] Please reset your password.", message.Subject);
                Assert.Contains("Click the following link within the next", message.Body);
                Assert.Contains("http://example.com/pwd-reset-token-url", message.Body);
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
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                var package = new PackageRegistration { Id = "CoolStuff" };
                var confirmationUrl = "http://example.com/confirmation-token-url";
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);

                Assert.Equal("new-owner@example.com", message.To[0].Address);
                Assert.Equal("existing-owner@example.com", message.From.Address);
                Assert.Equal("[NuGet Gallery] The user 'Existing' wants to add you as an owner of the package 'CoolStuff'.", message.Subject);
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
                var setting = new GallerySetting { GalleryOwnerName = "NuGet Gallery", GalleryOwnerEmail = "joe@example.com" };
                var messageService = new MessageService(mailSender.Object, setting);
                var package = new PackageRegistration { Id = "CoolStuff" };
                var confirmationUrl = "http://example.com/confirmation-token-url";
                MailMessage message = null;
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Callback<MailMessage>(m => { message = m; });

                messageService.SendPackageOwnerRequest(from, to, package, confirmationUrl);

                Assert.Null(message);
            }
        }
    }
}
