// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class CredentialRemovedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.ApiKeyCredentialTypeInfo };
                    yield return new object[] { Configuration, null, Fakes.ApiKeyCredentialTypeInfo };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User user,
                CredentialTypeInfo credentialTypeInfo)
            {
                Assert.Throws<ArgumentNullException>(() => new CredentialRemovedMessage(
                    configuration,
                    user,
                    credentialTypeInfo));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsUserEmailAddressToToList()
            {
                var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForApiKey)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForApiKey)]
            [InlineData(EmailFormat.Html, "<p>" + _expectedMessageBodyForApiKey + "</p>\n")]
            public void ReturnsExpectedBodyForApiKey(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForNonApiKey)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForNonApiKey)]
            [InlineData(EmailFormat.Html, "<p>" + _expectedMessageBodyForNonApiKey + "</p>\n")]
            public void ReturnsExpectedBodyForNonApiKey(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(Fakes.NonApiKeyCredentialTypeInfo);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryOwnerAsSender()
        {
            var message = CreateMessage(Fakes.ApiKeyCredentialTypeInfo);

            Assert.Equal(Configuration.GalleryOwner, message.Sender);
        }

        private static CredentialRemovedMessage CreateMessage(CredentialTypeInfo credentialTypeInfo)
        {
            return new CredentialRemovedMessage(
                Configuration,
                Fakes.RequestingUser,
                credentialTypeInfo);
        }

        private const string _expectedMessageBodyForApiKey =
            @"API key 'Api Key description' was removed from your account and can no longer be used. If you did not request this change, please reply to this email to contact support.";

        private const string _expectedMessageBodyForNonApiKey =
            @"A Microsoft Account was removed from your account and can no longer be used to sign in. If you did not request this change, please reply to this email to contact support.";
    }
}
