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
    public class SigninAssistanceMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser.ToMailAddress(), new List<Credential>() };
                    yield return new object[] { Configuration, null, new List<Credential>() };
                    yield return new object[] { Configuration, Fakes.RequestingUser.ToMailAddress(), null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                MailAddress toAddress,
                IEnumerable<Credential> credentials)
            {
                Assert.Throws<ArgumentNullException>(() => new SigninAssistanceMessage(
                    configuration,
                    toAddress,
                    credentials));
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
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForNoCredentials, false)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForNoCredentials, false)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForNoCredentials, false)]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForCredentials, true)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForCredentials, true)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForCredentials, true)]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString, bool hasCredentials)
            {
                var message = CreateMessage(hasCredentials);

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

        private static SigninAssistanceMessage CreateMessage(bool hasCredentials = false)
        {
            var credentials = new List<Credential>();
            if (hasCredentials)
            {
                credentials.Add(new Credential
                {
                    Type = CredentialTypes.ApiKey.Prefix,
                    Value = Guid.Empty.ToString(),
                    Identity = "Credential identity"
                });
            }

            return new SigninAssistanceMessage(
                Configuration,
                Fakes.RequestingUser.ToMailAddress(),
                credentials);
        }

        private const string _expectedMessageBodyForNoCredentials =
            @"Hi there,

We heard you were looking for Microsoft logins associated with your account on NuGetGallery.

No associated Microsoft logins were found.

Thanks,

The NuGetGallery Team";

        private const string _expectedMessageBodyForCredentials =
            @"Hi there,

We heard you were looking for Microsoft logins associated with your account on NuGetGallery.

Our records indicate the associated Microsoft login(s): Credential identity.

Thanks,

The NuGetGallery Team";

        private const string _expectedHtmlBodyForNoCredentials =
            "<p>Hi there,</p>\n" +
"<p>We heard you were looking for Microsoft logins associated with your account on NuGetGallery.</p>\n" +
"<p>No associated Microsoft logins were found.</p>\n" +
"<p>Thanks,</p>\n" +
"<p>The NuGetGallery Team</p>\n";

        private const string _expectedHtmlBodyForCredentials =
            "<p>Hi there,</p>\n" +
"<p>We heard you were looking for Microsoft logins associated with your account on NuGetGallery.</p>\n" +
"<p>Our records indicate the associated Microsoft login(s): Credential identity.</p>\n" +
"<p>Thanks,</p>\n" +
"<p>The NuGetGallery Team</p>\n";
    }
}
