// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using Moq;
using Xunit;

namespace NuGet.Services.Messaging.Email.Tests
{
    public class AsynchronousEmailMessageServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenANullEnqueuer_ItShouldThrow()
            {
                Assert.Throws<ArgumentNullException>(() => new AsynchronousEmailMessageService(null));
            }
        }

        public class TheSendMessageAsyncMethod
        {
            [Fact]
            public void ThrowsArgumentNullExceptionForNullEmailBuilder()
            {
                var emailMessageEnqueuer = new Mock<IEmailMessageEnqueuer>().Object;
                var messageService = new AsynchronousEmailMessageService(emailMessageEnqueuer);

                Assert.ThrowsAsync<ArgumentNullException>(() => messageService.SendMessageAsync(null, It.IsAny<bool>(), It.IsAny<bool>()));
            }

            [Fact]
            public void DoesNotEnqueueMessageWhenRecipientsToListEmpty()
            {
                var emailBuilder = new Mock<IEmailBuilder>();
                emailBuilder
                    .Setup(m => m.GetRecipients())
                    .Returns(EmailRecipients.None)
                    .Verifiable();

                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(emailMessageEnqueuerMock.Object);

                messageService.SendMessageAsync(emailBuilder.Object, false, false);

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public void ThrowsArgumentExceptionWhenPlainTextAndHtmlBodyEmpty()
            {
                var recipients = new EmailRecipients(
                    to: new[] { new MailAddress("to@gallery.org") });
                var emailBuilder = new Mock<IEmailBuilder>();
                emailBuilder
                     .Setup(m => m.Sender)
                     .Returns(new MailAddress("sender@gallery.org"))
                     .Verifiable();
                emailBuilder
                    .Setup(m => m.GetRecipients())
                    .Returns(recipients)
                    .Verifiable();
                emailBuilder
                    .Setup(m => m.GetSubject())
                    .Returns("subject")
                    .Verifiable();
                emailBuilder
                     .Setup(m => m.GetBody(EmailFormat.Html))
                     .Returns<string>(null)
                     .Verifiable();
                emailBuilder
                     .Setup(m => m.GetBody(EmailFormat.PlainText))
                     .Returns<string>(null)
                     .Verifiable();

                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(emailMessageEnqueuerMock.Object);

                Assert.ThrowsAsync<ArgumentException>(() => messageService.SendMessageAsync(emailBuilder.Object, false, false));

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public void ThrowsArgumentExceptionWhenSenderNull()
            {
                var recipients = new EmailRecipients(
                    to: new[] { new MailAddress("to@gallery.org") });
                var emailBuilder = new Mock<IEmailBuilder>();
                emailBuilder
                    .Setup(m => m.GetRecipients())
                    .Returns(recipients)
                    .Verifiable();
                emailBuilder
                     .Setup(m => m.Sender)
                     .Returns<MailAddress>(null)
                     .Verifiable();

                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(emailMessageEnqueuerMock.Object);

                Assert.ThrowsAsync<ArgumentException>(() => messageService.SendMessageAsync(emailBuilder.Object, false, false));

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public void CreatesAndSendsExpectedMessageOnce()
            {
                var subject = "subject";
                var htmlBody = "html body";
                var plainTextBody = "plain-text body";
                var toAddress = "to@gallery.org";
                var ccAddress = "cc@gallery.org";
                var bccAddress = "bcc@gallery.org";
                var replyToAddress = "replyto@gallery.org";
                var senderAddress = "sender@gallery.org";

                var recipients = new EmailRecipients(
                    to: new[] { new MailAddress(toAddress) },
                    cc: new[] { new MailAddress(ccAddress) },
                    bcc: new[] { new MailAddress(bccAddress) },
                    replyTo: new[] { new MailAddress(replyToAddress) });
                var emailBuilder = new Mock<IEmailBuilder>();
                emailBuilder
                    .Setup(m => m.GetRecipients())
                    .Returns(recipients)
                    .Verifiable();
                emailBuilder
                    .Setup(m => m.GetSubject())
                    .Returns(subject)
                    .Verifiable();
                emailBuilder
                     .Setup(m => m.GetBody(EmailFormat.Html))
                     .Returns(htmlBody)
                     .Verifiable();
                emailBuilder
                     .Setup(m => m.GetBody(EmailFormat.PlainText))
                     .Returns(plainTextBody)
                     .Verifiable();
                emailBuilder
                     .Setup(m => m.Sender)
                     .Returns(new MailAddress(senderAddress))
                     .Verifiable();

                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(emailMessageEnqueuerMock.Object);

                messageService.SendMessageAsync(emailBuilder.Object, false, false);

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.Is<EmailMessageData>(
                        d =>
                        d.HtmlBody == htmlBody
                        && d.PlainTextBody == plainTextBody
                        && d.Subject == subject
                        && d.Sender == senderAddress
                        && d.To.Contains(toAddress)
                        && d.CC.Contains(ccAddress)
                        && d.Bcc.Contains(bccAddress)
                        && d.ReplyTo.Contains(replyToAddress)
                        && d.MessageTrackingId != Guid.Empty
                        && d.DeliveryCount == 0)),
                    Times.Once);
            }
        }
    }
}