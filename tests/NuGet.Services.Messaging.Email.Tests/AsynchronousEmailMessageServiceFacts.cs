// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
                Assert.Throws<ArgumentNullException>(() => new AsynchronousEmailMessageService(
                    null,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>()));
            }

            [Fact]
            public void GivenANullLogger_ItShouldThrow()
            {
                Assert.Throws<ArgumentNullException>(() => new AsynchronousEmailMessageService(
                    Mock.Of<IEmailMessageEnqueuer>(),
                    null,
                    Mock.Of<IMessageServiceConfiguration>()));
            }

            [Fact]
            public void GivenANullConfiguration_ItShouldThrow()
            {
                Assert.Throws<ArgumentNullException>(() => new AsynchronousEmailMessageService(
                    Mock.Of<IEmailMessageEnqueuer>(),
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    null));
            }
        }

        public class TheSendMessageAsyncMethod
        {
            [Fact]
            public async Task ThrowsArgumentNullExceptionForNullEmailBuilder()
            {
                var emailMessageEnqueuer = new Mock<IEmailMessageEnqueuer>().Object;
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuer,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>());

                await Assert.ThrowsAsync<ArgumentNullException>(() => messageService.SendMessageAsync(null, It.IsAny<bool>(), It.IsAny<bool>()));
            }

            [Fact]
            public async Task DoesNotEnqueueMessageWhenRecipientsToListEmpty()
            {
                var emailBuilder = new Mock<IEmailBuilder>();
                emailBuilder
                    .Setup(m => m.GetRecipients())
                    .Returns(new EmailRecipients(to: new MailAddress[0]))
                    .Verifiable();

                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuerMock.Object,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>());

                await messageService.SendMessageAsync(emailBuilder.Object, false, false);

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task ThrowsArgumentExceptionWhenPlainTextAndHtmlBodyEmpty()
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
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuerMock.Object,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>());

                await Assert.ThrowsAsync<ArgumentNullException>(() => messageService.SendMessageAsync(emailBuilder.Object, false, false));

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task ThrowsArgumentExceptionWhenSenderNull()
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
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuerMock.Object,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>());

                await Assert.ThrowsAsync<ArgumentException>(() => messageService.SendMessageAsync(emailBuilder.Object, false, false));

                emailBuilder.Verify();
                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()),
                    Times.Never);
            }

            [Fact]
            public async Task CreatesAndSendsExpectedMessageOnce()
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
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuerMock.Object,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    Mock.Of<IMessageServiceConfiguration>());

                await messageService.SendMessageAsync(emailBuilder.Object, false, false);

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

                emailMessageEnqueuerMock.Verify(m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()), Times.Once);
            }

            [Fact]
            public async Task WillSendCopyToSenderIfAsked()
            {
                var subject = "subject";
                var htmlBody = "html body";
                var plainTextBody = "plain-text body";
                var toAddress = "to@gallery.org";
                var ccAddress = "cc@gallery.org";
                var bccAddress = "bcc@gallery.org";
                var fromAddress = "fromAddress@gallery.org";
                var senderAddress = "sender@gallery.org";

                var recipients = new EmailRecipients(
                    to: new[] { new MailAddress(toAddress) },
                    cc: new[] { new MailAddress(ccAddress) },
                    bcc: new[] { new MailAddress(bccAddress) },
                    replyTo: new[] { new MailAddress(fromAddress) });
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

                var messageServiceConfiguration = new TestMessageServiceConfiguration();
                var emailMessageEnqueuerMock = new Mock<IEmailMessageEnqueuer>();
                var messageService = new AsynchronousEmailMessageService(
                    emailMessageEnqueuerMock.Object,
                    Mock.Of<ILogger<AsynchronousEmailMessageService>>(),
                    messageServiceConfiguration);

                // We want to copy the sender but not disclose the sender address
                await messageService.SendMessageAsync(
                    emailBuilder.Object,
                    copySender: true,
                    discloseSenderAddress: false);

                emailBuilder.Verify();

                // Verify the original email is sent (not disclosing the sender address)
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
                        && d.ReplyTo.Contains(fromAddress)
                        && d.MessageTrackingId != Guid.Empty
                        && d.DeliveryCount == 0)),
                    Times.Once);

                // Verify a copy is sent to the sender
                var expectedPlainTextBody = string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        messageServiceConfiguration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        plainTextBody);

                var expectedHtmlBody = string.Format(
                            CultureInfo.CurrentCulture,
                            "You sent the following message via {0}: {1}{1}{2}",
                            messageServiceConfiguration.GalleryOwner.DisplayName,
                            Environment.NewLine,
                            htmlBody);

                emailMessageEnqueuerMock.Verify(
                    m => m.SendEmailMessageAsync(It.Is<EmailMessageData>(
                        d =>
                        d.HtmlBody == expectedHtmlBody
                        && d.PlainTextBody == expectedPlainTextBody
                        && d.Subject == subject + " [Sender Copy]"
                        && d.Sender == messageServiceConfiguration.GalleryOwner.Address
                        && d.To.Single() == fromAddress
                        && !d.CC.Any()
                        && !d.Bcc.Any()
                        && d.ReplyTo.Single() == fromAddress
                        && d.MessageTrackingId != Guid.Empty
                        && d.DeliveryCount == 0)),
                    Times.Once);

                emailMessageEnqueuerMock.Verify(m => m.SendEmailMessageAsync(It.IsAny<EmailMessageData>()), Times.Exactly(2));
            }
        }
    }
}