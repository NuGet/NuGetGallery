using System;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;
using Moq;
using Xunit;

namespace NuGetGallery.Services {
    public class MessageServiceFacts {
        public class TheSendContactOwnersMessageMethod {
            [Fact]
            public void WillSendEmailToAllOwners() {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = true },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = true }
                };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.GalleryOwnerEmailAddress).Returns(new MailAddress("Joe Schmoe <joe@example.com>"));
                var messageService = new MessageService(mailSender.Object, config.Object);

                var message = messageService.SendContactOwnersMessage(from, package, "Test message");

                mailSender.Verify(m => m.Send(It.IsAny<MailMessage>()));
                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal("flynt@example.com", message.To[1].Address);
                Assert.Contains("NuGet.org: Message for owners of the package 'smangit'", message.Subject);
                Assert.Contains("Test message", message.Body);
                Assert.Contains("User flossy (smangit@example.com) sends the following message to the owners of Package 'smangit'.", message.Body);
            }

            [Fact]
            public void WillNotSendEmailToOwnerThatOptsOut() {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = true },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = false }
                };
                var mailSender = new Mock<IMailSender>();
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.GalleryOwnerEmailAddress).Returns(new MailAddress("Joe Schmoe <joe@example.com>"));
                var messageService = new MessageService(mailSender.Object, config.Object);

                var message = messageService.SendContactOwnersMessage(from, package, "Test message");

                Assert.Equal("yung@example.com", message.To[0].Address);
                Assert.Equal(1, message.To.Count);
            }

            [Fact]
            public void WillNotAttemptToSendIfNoOwnersAllow() {
                var from = new MailAddress("smangit@example.com", "flossy");
                var package = new PackageRegistration { Id = "smangit" };
                package.Owners = new[] {
                    new User {EmailAddress = "yung@example.com", EmailAllowed = false },
                    new User {EmailAddress = "flynt@example.com", EmailAllowed = false }
                };
                var mailSender = new Mock<IMailSender>();
                mailSender.Setup(m => m.Send(It.IsAny<MailMessage>())).Throws(new InvalidOperationException());
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.GalleryOwnerEmailAddress).Returns(new MailAddress("Joe Schmoe <joe@example.com>"));
                var messageService = new MessageService(mailSender.Object, config.Object);

                var message = messageService.SendContactOwnersMessage(from, package, "Test message");

                Assert.Equal(0, message.To.Count);
            }
        }

        public class TheReportAbuseMethod {
            [Fact]
            public void WillSendEmailToGalleryOwner() {
                var from = new MailAddress("legit@example.com", "too");
                var package = new Package {
                    PackageRegistration = new PackageRegistration { Id = "smangit" },
                    Version = "1.42.0.1"
                };
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.GalleryOwnerEmailAddress).Returns(new MailAddress("Joe Schmoe <joe@example.com>"));
                var mailSender = new Mock<IMailSender>();
                var messageService = new MessageService(mailSender.Object, config.Object);

                var message = messageService.ReportAbuse(from, package, "Abuse!");

                Assert.Equal("joe@example.com", message.To[0].Address);
                Assert.Equal("Abuse Report for Package 'smangit' Version '1.42.0.1'", message.Subject);
                Assert.Contains("Abuse!", message.Body);
                Assert.Contains("User too (legit@example.com) reports the package 'smangit' version '1.42.0.1' as abusive", message.Body);
            }
        }

        public class TheSendNewAccountEmailMethod {
            [Fact]
            public void WillSendEmailToNewUser() {
                var to = new MailAddress("legit@example.com", "too");
                var config = new Mock<IConfiguration>();
                config.Setup(c => c.GalleryOwnerEmailAddress).Returns(new MailAddress("Joe Schmoe <joe@example.com>"));
                var mailSender = new Mock<IMailSender>();
                var messageService = new MessageService(mailSender.Object, config.Object);

                var message = messageService.SendNewAccountEmail(to, "http://example.com/confirmation-token-url");

                Assert.Equal("legit@example.com", message.To[0].Address);
                Assert.Equal("Please verify your NuGet.org account.", message.Subject);
                Assert.Contains("http://example.com/confirmation-token-url", message.Body);
            }
        }
    }
}
