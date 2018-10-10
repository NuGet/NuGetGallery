﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestInitiatedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingOrganization, Fakes.RequestingUser, Fakes.RequestingUser, It.IsAny<bool>(), Fakes.CancellationUrl };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser, Fakes.RequestingUser, It.IsAny<bool>(), Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, null, Fakes.RequestingUser, It.IsAny<bool>(), Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, null, It.IsAny<bool>(), Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, null, It.IsAny<bool>(), null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Organization organization,
                User requestingUser,
                User pendingUser,
                bool isAdmin,
                string cancellationUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationMembershipRequestInitiatedMessage(
                    configuration,
                    organization,
                    requestingUser,
                    pendingUser,
                    isAdmin,
                    cancellationUrl));
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
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForAdmin)]
            public void ReturnsExpectedBodyForAdmin(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(isAdmin: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForNonAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForNonAdmin)]
            public void ReturnsExpectedBodyForNonAdmin(EmailFormat format, string expectedString)
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

        private static OrganizationMembershipRequestInitiatedMessage CreateMessage(
            Organization organization = null,
            bool pendingUserEmailAllowed = true,
            bool isAdmin = false)
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

            return new OrganizationMembershipRequestInitiatedMessage(
                Configuration,
                organization,
                Fakes.RequestingUser,
                pendingUser,
                isAdmin,
                Fakes.CancellationUrl);
        }

        private const string _expectedMessageBodyForAdmin =
            @"The user 'requestingUser' has requested that user 'requestingUser' be added as an administrator of organization 'requestingOrganization'. A confirmation mail has been sent to user 'requestingUser' to accept the membership request. This mail is to inform you of the membership changes to organization 'requestingOrganization' and there is no action required from you.

Thanks,
The NuGetGallery Team";
        private const string _expectedMessageBodyForNonAdmin =
            @"The user 'requestingUser' has requested that user 'requestingUser' be added as a collaborator of organization 'requestingOrganization'. A confirmation mail has been sent to user 'requestingUser' to accept the membership request. This mail is to inform you of the membership changes to organization 'requestingOrganization' and there is no action required from you.

Thanks,
The NuGetGallery Team";
    }
}
