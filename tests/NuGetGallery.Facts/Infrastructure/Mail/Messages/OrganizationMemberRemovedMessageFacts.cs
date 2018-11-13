// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberRemovedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingOrganization, Fakes.RequestingUser };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Organization organization,
                User removedUser)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationMemberRemovedMessage(
                    configuration,
                    organization,
                    removedUser));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsOrganizationMailAddressToToListWhenOrganizationEmailAllowed()
            {
                var message = CreateMessage(organizationEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingOrganization.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void ReturnsRecipientsNoneWhenOrganizationEmailNotAllowed()
            {
                var message = CreateMessage(organizationEmailAllowed: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(organizationEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(organizationEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void AddsRemovedUserToReplyToList()
            {
                var message = CreateMessage(organizationEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.ReplyTo);
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
                var message = CreateMessage(organizationEmailAllowed: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage(organizationEmailAllowed: true);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static OrganizationMemberRemovedMessage CreateMessage(bool organizationEmailAllowed)
        {
            var organization = Fakes.RequestingOrganization;
            organization.EmailAllowed = organizationEmailAllowed;

            return new OrganizationMemberRemovedMessage(
                Configuration,
                organization,
                Fakes.RequestingUser);
        }

        private const string _expectedMessageBody =
            @"The user 'requestingUser' is no longer a member of organization 'requestingOrganization'.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>The user 'requestingUser' is no longer a member of organization 'requestingOrganization'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
