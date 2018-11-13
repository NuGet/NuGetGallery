// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberUpdatedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingOrganization, Fakes.OrganizationMembership };
                    yield return new object[] { Configuration, null, Fakes.OrganizationMembership };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Organization organization,
                Membership membership)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationMemberUpdatedMessage(
                    configuration,
                    organization,
                    membership));
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
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyNonAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyNonAdmin)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyNonAdmin)]
            public void ReturnsExpectedBodyForNonAdmin(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(organizationEmailAllowed: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyAdmin)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyAdmin)]
            public void ReturnsExpectedBodyForAdmin(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(organizationEmailAllowed: true, isAdmin: true);

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

        private static OrganizationMemberUpdatedMessage CreateMessage(
            bool organizationEmailAllowed,
            bool isAdmin = false)
        {
            var organization = Fakes.RequestingOrganization;
            organization.EmailAllowed = organizationEmailAllowed;

            var membership = Fakes.OrganizationMembership;
            membership.IsAdmin = isAdmin;

            return new OrganizationMemberUpdatedMessage(
                Configuration,
                organization,
                membership);
        }

        private const string _expectedMessageBodyNonAdmin =
            @"The user 'requestingUser' is now a collaborator of organization 'requestingOrganization'.

Thanks,
The NuGetGallery Team";

        private const string _expectedMessageBodyAdmin =
            @"The user 'requestingUser' is now an administrator of organization 'requestingOrganization'.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBodyNonAdmin =
            "<p>The user 'requestingUser' is now a collaborator of organization 'requestingOrganization'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";

        private const string _expectedHtmlBodyAdmin =
            "<p>The user 'requestingUser' is now an administrator of organization 'requestingOrganization'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
