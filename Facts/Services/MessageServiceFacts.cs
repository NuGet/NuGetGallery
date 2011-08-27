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
                var messageService = new MessageService(mailSender.Object);

                var message = messageService.SendContactOwnersMessage(from, package, "Test message");

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
                var messageService = new MessageService(mailSender.Object);

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
                var messageService = new MessageService(mailSender.Object);

                var message = messageService.SendContactOwnersMessage(from, package, "Test message");

                Assert.Equal(0, message.To.Count);
            }
        }
    }
}
