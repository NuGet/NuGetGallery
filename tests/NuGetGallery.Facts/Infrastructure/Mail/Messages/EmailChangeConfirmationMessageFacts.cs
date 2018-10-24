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
    public class EmailChangeConfirmationMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.UnconfirmedUser, Fakes.ConfirmationUrl };
                    yield return new object[] { Configuration, null, Fakes.ConfirmationUrl };
                    yield return new object[] { Configuration, Fakes.UnconfirmedUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User user,
                string confirmationUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new EmailChangeConfirmationMessage(
                    configuration,
                    user,
                    confirmationUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsUserMailAddressToToList()
            {
                var user = Fakes.UnconfirmedUser;
                var expectedMailAddress = new MailAddress(user.UnconfirmedEmailAddress, user.Username);
                var message = CreateMessage(user);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(expectedMailAddress, recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(Fakes.UnconfirmedUser);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(Fakes.UnconfirmedUser);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage(Fakes.UnconfirmedUser);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyForUser)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyForUser)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForUser)]
            public void ReturnsExpectedBodyForUser(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(Fakes.UnconfirmedUser);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyForOrganization)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyForOrganization)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForOrganization)]
            public void ReturnsExpectedBodyForOrganization(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(Fakes.UnconfirmedOrganization);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage(Fakes.UnconfirmedUser);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static EmailChangeConfirmationMessage CreateMessage(User requestingUser)
        {
            return new EmailChangeConfirmationMessage(
                Configuration,
                requestingUser,
                Fakes.ConfirmationUrl);
        }

        private const string _expectedMarkdownBodyForUser =
            @"You recently changed your account's NuGetGallery email address.

To verify account new email address:

[confirmationUrl](confirmationUrl)

Thanks,
The NuGetGallery Team";

        private const string _expectedPlainTextBodyForUser =
            @"You recently changed your account's NuGetGallery email address.

To verify account new email address:

confirmationUrl

Thanks,
The NuGetGallery Team";

        private const string _expectedMarkdownBodyForOrganization =
            @"You recently changed your organization's NuGetGallery email address.

To verify organization new email address:

[confirmationUrl](confirmationUrl)

Thanks,
The NuGetGallery Team";

        private const string _expectedPlainTextBodyForOrganization =
            @"You recently changed your organization's NuGetGallery email address.

To verify organization new email address:

confirmationUrl

Thanks,
The NuGetGallery Team";
        private const string _expectedHtmlBodyForUser =
            "<p>You recently changed your account's NuGetGallery email address.</p>\n" +
"<p>To verify account new email address:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
        private const string _expectedHtmlBodyForOrganization =
            "<p>You recently changed your organization's NuGetGallery email address.</p>\n" +
"<p>To verify organization new email address:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
