// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class AccountDeleteNoticeMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser };
                    yield return new object[] { Configuration, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User user)
            {
                Assert.Throws<ArgumentNullException>(() => new AccountDeleteNoticeMessage(
                    configuration,
                    user));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsUserMailAddressToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.To);
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
            [InlineData(EmailFormat.Markdown, _expectedMessageBody)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBody)]
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

        private static AccountDeleteNoticeMessage CreateMessage()
        {
            return new AccountDeleteNoticeMessage(
                Configuration,
                Fakes.RequestingUser);
        }

        private const string _expectedMessageBody =
            @"We received a request to delete your account requestingUser. If you did not initiate this request, please contact the NuGetGallery team immediately.

When your account will be deleted, we will:

- revoke your API key(s)
- remove you as the owner for any package you own
- remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of

We will not delete the NuGet packages associated with the account.

Thanks,

The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>We received a request to delete your account requestingUser. If you did not initiate this request, please contact the NuGetGallery team immediately.</p>\n" +
"<p>When your account will be deleted, we will:</p>\n" +
"<ul>\n" +
"<li>revoke your API key(s)</li>\n" +
"<li>remove you as the owner for any package you own</li>\n" +
"<li>remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of</li>\n" +
"</ul>\n" +
"<p>We will not delete the NuGet packages associated with the account.</p>\n" +
"<p>Thanks,</p>\n" +
"<p>The NuGetGallery Team</p>\n";
    }
}
