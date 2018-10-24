// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class EmailChangeNoticeToPreviousEmailAddressMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.UnconfirmedUser, Fakes.PreviousEmailAddress };
                    yield return new object[] { Configuration, null, Fakes.PreviousEmailAddress };
                    yield return new object[] { Configuration, Fakes.UnconfirmedUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User user,
                string previousEmailAddress)
            {
                Assert.Throws<ArgumentNullException>(() => new EmailChangeNoticeToPreviousEmailAddressMessage(
                    configuration,
                    user,
                    previousEmailAddress));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsUserMailAddressToToList()
            {
                var user = Fakes.RequestingUser;
                var expectedMailAddress = new MailAddress(Fakes.PreviousEmailAddress, user.Username);
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(expectedMailAddress, recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage();
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

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
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
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage();

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static EmailChangeNoticeToPreviousEmailAddressMessage CreateMessage()
        {
            return new EmailChangeNoticeToPreviousEmailAddressMessage(
                Configuration,
                Fakes.RequestingUser,
                Fakes.PreviousEmailAddress);
        }

        private const string _expectedMarkdownBody =
            @"The email address associated with your NuGetGallery account was recently changed from _previousAddress@gallery.org_ to _requestUser@gallery.org_.

Thanks,
The NuGetGallery Team";

        private const string _expectedPlainTextBody =
            @"The email address associated with your NuGetGallery account was recently changed from previousAddress@gallery.org to requestUser@gallery.org.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>The email address associated with your NuGetGallery account was recently changed from <em>previousAddress@gallery.org</em> to <em>requestUser@gallery.org</em>.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
