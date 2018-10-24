// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerRemovedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.OrganizationAdmin, Fakes.RequestingUser, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, Fakes.OrganizationAdmin, null, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, Fakes.OrganizationAdmin, Fakes.RequestingUser, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User fromUser,
                User toUser,
                PackageRegistration packageRegistration)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageOwnerRemovedMessage(
                    configuration,
                    fromUser,
                    toUser,
                    packageRegistration));
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

                var message = CreateMessage(organization, isOrganization: true);
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
            public void AddsFromUserAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.RequestingOrganization.ToMailAddress(), recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForUser)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForUser)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForUser)]
            public void ReturnsExpectedBodyForUser(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(isOrganization: false);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForOrganization)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForOrganization)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForOrganization)]
            public void ReturnsExpectedBodyForOrganization(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(isOrganization: true);

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

        private static PackageOwnerRemovedMessage CreateMessage(
            Organization organization = null,
            bool isOrganization = false)
        {
            if (organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }

            return new PackageOwnerRemovedMessage(
                Configuration,
                organization,
                isOrganization ? organization : Fakes.OrganizationAdmin,
                Fakes.Package.PackageRegistration);
        }

        private const string _expectedMessageBodyForUser =
            @"The user 'requestingOrganization' removed you as an owner of the package 'PackageId'.

Thanks,
The NuGetGallery Team";

        private const string _expectedMessageBodyForOrganization =
            @"The user 'requestingOrganization' removed your organization as an owner of the package 'PackageId'.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBodyForUser =
            "<p>The user 'requestingOrganization' removed you as an owner of the package 'PackageId'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";

        private const string _expectedHtmlBodyForOrganization =
            "<p>The user 'requestingOrganization' removed your organization as an owner of the package 'PackageId'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
