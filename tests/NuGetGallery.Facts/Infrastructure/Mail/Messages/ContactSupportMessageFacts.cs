// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactSupportMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, new MailAddress("someone@domain.tld"), Fakes.RequestingUser, "message", "reason", It.IsAny<bool>() };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser, "message", "reason", It.IsAny<bool>() };
                    yield return new object[] { Configuration, new MailAddress("someone@domain.tld"), null, "message", "reason", It.IsAny<bool>() };
                    yield return new object[] { Configuration, new MailAddress("someone@domain.tld"), Fakes.RequestingUser, null, "reason", It.IsAny<bool>() };
                    yield return new object[] { Configuration, new MailAddress("someone@domain.tld"), Fakes.RequestingUser, "message", null, It.IsAny<bool>() };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                MailAddress fromAddress,
                User requestingUser,
                string message,
                string reason,
                bool copySender)
            {
                Assert.Throws<ArgumentNullException>(() => new ContactSupportMessage(
                    configuration,
                    fromAddress,
                    requestingUser,
                    message,
                    reason,
                    copySender));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsGalleryOwnerToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Configuration.GalleryOwner, recipients.To);
            }

            [Fact]
            public void AddsFromAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.FromAddress, recipients.ReplyTo);
            }

            [Fact]
            public void AddsFromAddressToCCListWhenCopyingSender()
            {
                var message = CreateMessage(copySender: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.CC.Count);
                Assert.Contains(Fakes.FromAddress, recipients.CC);
            }

            [Fact]
            public void DoesNotAddFromAddressToCCListWhenNotCopyingSender()
            {
                var message = CreateMessage(copySender: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBody)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBody)]
            [InlineData(EmailFormat.Html, _expectedHtmlBody)]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString)
            {
                var message = CreateMessage();

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryOwnerAsSender()
        {
            var message = CreateMessage();

            Assert.Equal(Configuration.GalleryOwner, message.Sender);
        }

        private static ContactSupportMessage CreateMessage(bool copySender = false)
        {
            return new ContactSupportMessage(
                Configuration,
                Fakes.FromAddress,
                Fakes.RequestingUser,
                "message",
                "reason",
                copySender);
        }

        private const string _expectedMarkdownBody =
            @"**Email:** requestingUser (requestUser@gallery.org)

**Reason:**
reason

**Message:**
message";

        private const string _expectedPlainTextBody =
            @"Email: requestingUser (requestUser@gallery.org)

Reason:
reason

Message:
message";

        private const string _expectedHtmlBody =
            "<p><strong>Email:</strong> requestingUser (requestUser@gallery.org)</p>\n" +
            "<p><strong>Reason:</strong>\n" +
            "reason</p>\n" +
            "<p><strong>Message:</strong>\n" +
            "message</p>\n";
    }
}