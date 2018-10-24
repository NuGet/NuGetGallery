// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class NewAccountMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.ConfirmationUrl };
                    yield return new object[] { Configuration, null, Fakes.ConfirmationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User user,
                string confirmationUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new NewAccountMessage(
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
                var message = CreateMessage(Fakes.RequestingUser);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(Fakes.RequestingUser);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(Fakes.RequestingUser);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage(Fakes.RequestingUser);
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
                var message = CreateMessage(Fakes.RequestingUser);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyForOrganization)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyForOrganization)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForOrganization)]
            public void ReturnsExpectedBodyForOrganization(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(Fakes.RequestingOrganization);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage(Fakes.RequestingUser);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static NewAccountMessage CreateMessage(User requestingUser)
        {
            return new NewAccountMessage(
                Configuration,
                requestingUser,
                Fakes.ConfirmationUrl);
        }

        private const string _expectedMarkdownBodyForUser =
            @"Thank you for registering with the NuGetGallery.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

[confirmationUrl](confirmationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBodyForUser =
            @"Thank you for registering with the NuGetGallery.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

confirmationUrl

Thanks,
The NuGetGallery Team";
        private const string _expectedMarkdownBodyForOrganization =
            @"Thank you for creating an organization on the NuGetGallery.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

[confirmationUrl](confirmationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBodyForOrganization =
            @"Thank you for creating an organization on the NuGetGallery.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

confirmationUrl

Thanks,
The NuGetGallery Team";
        private const string _expectedHtmlBodyForUser =
            "<p>Thank you for registering with the NuGetGallery.\n" +
"We can't wait to see what packages you'll upload.</p>\n" +
"<p>So we can be sure to contact you, please verify your email address using the following link:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
        private const string _expectedHtmlBodyForOrganization =
            "<p>Thank you for creating an organization on the NuGetGallery.\n" +
"We can't wait to see what packages you'll upload.</p>\n" +
"<p>So we can be sure to contact you, please verify your email address using the following link:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
