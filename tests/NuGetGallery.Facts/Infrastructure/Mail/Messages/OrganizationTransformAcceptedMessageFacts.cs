// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformAcceptedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User accountToTransform,
                User adminUser)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationTransformAcceptedMessage(
                    configuration,
                    accountToTransform,
                    adminUser));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsAccountToTransformMailAddressToToListWhenAccountToTransformEmailAllowed()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void ReturnsRecipientsNoneWhenAccountToTransformEmailNotAllowed()
            {
                var message = CreateMessage(accountToTransformEmailAllowed: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.To);
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
            public void AddsAdminUserToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.OrganizationAdmin.ToMailAddress(), recipients.ReplyTo);
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
        public void SetsGalleryOwnerAsSender()
        {
            var message = CreateMessage();

            Assert.Equal(Configuration.GalleryOwner, message.Sender);
        }

        private static OrganizationTransformAcceptedMessage CreateMessage(bool accountToTransformEmailAllowed = true)
        {
            var accountToTransform = Fakes.RequestingUser;
            accountToTransform.EmailAllowed = accountToTransformEmailAllowed;

            return new OrganizationTransformAcceptedMessage(
                Configuration,
                accountToTransform,
                Fakes.OrganizationAdmin);
        }

        private const string _expectedMessageBody =
            @"Account 'requestingUser' has been transformed into an organization with user 'organizationAdmin' as its administrator. If you did not request this change, please contact support by responding to this email.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>Account 'requestingUser' has been transformed into an organization with user 'organizationAdmin' as its administrator. If you did not request this change, please contact support by responding to this email.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
