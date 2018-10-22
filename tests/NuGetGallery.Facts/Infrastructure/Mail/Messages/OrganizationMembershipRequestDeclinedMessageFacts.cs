// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestDeclinedMessageFacts : MarkdownMessageBuilderFacts
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
                User pendingUser)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationMembershipRequestDeclinedMessage(
                    configuration,
                    organization,
                    pendingUser));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsOrganizationMembersWithPermissionToToList()
            {
                var organizationAdmin = new User("organizationAdmin")
                {
                    EmailAddress = "organizationAdmin@gallery.org",
                    EmailAllowed = true
                };
                var organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
                var membership = new Membership
                {
                    Member = organizationAdmin,
                    Organization = organization,
                    IsAdmin = true
                };
                organization.Members.Add(membership);

                var message = CreateMessage(organization);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(organizationAdmin.ToMailAddress(), recipients.To);
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
            public void AddsPendingUserAddressToReplyToList()
            {
                var message = CreateMessage();
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

        private static OrganizationMembershipRequestDeclinedMessage CreateMessage(
            Organization organization = null,
            bool pendingUserEmailAllowed = true)
        {
            if (organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }

            var pendingUser = Fakes.RequestingUser;
            pendingUser.EmailAllowed = pendingUserEmailAllowed;

            return new OrganizationMembershipRequestDeclinedMessage(
                Configuration,
                organization,
                pendingUser);
        }

        private const string _expectedMessageBody =
            @"The user 'requestingUser' has declined your request to become a member of your organization.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>The user 'requestingUser' has declined your request to become a member of your organization.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
