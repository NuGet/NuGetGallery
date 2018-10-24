// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerAddedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.OrganizationAdmin, Fakes.RequestingUser, Fakes.Package.PackageRegistration, Fakes.PackageUrl };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser, Fakes.Package.PackageRegistration, Fakes.PackageUrl };
                    yield return new object[] { Configuration, Fakes.OrganizationAdmin, null, Fakes.Package.PackageRegistration, Fakes.PackageUrl };
                    yield return new object[] { Configuration, Fakes.OrganizationAdmin, Fakes.RequestingUser, null, Fakes.PackageUrl };
                    yield return new object[] { Configuration, Fakes.OrganizationAdmin, Fakes.RequestingUser, Fakes.Package.PackageRegistration, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User toUser,
                User newUser,
                PackageRegistration packageRegistration,
                string packageUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageOwnerAddedMessage(
                    configuration,
                    toUser,
                    newUser,
                    packageRegistration,
                    packageUrl));
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
            public void AddsGalleryNoReplyAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Configuration.GalleryNoReplyAddress, recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBody)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBody)]
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

        private static PackageOwnerAddedMessage CreateMessage(Organization organization = null)
        {
            if (organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }

            return new PackageOwnerAddedMessage(
                Configuration,
                organization,
                Fakes.RequestingUser,
                Fakes.Package.PackageRegistration,
                Fakes.PackageUrl);
        }

        private const string _expectedMarkdownBody =
            @"User 'requestingUser' is now an owner of the package ['PackageId'](packageUrl).

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBody =
            @"User 'requestingUser' is now an owner of the package 'PackageId' (packageUrl).

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>User 'requestingUser' is now an owner of the package <a href=\"packageUrl\">'PackageId'</a>.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
