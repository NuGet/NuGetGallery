// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestDeclinedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null, Fakes.Package.PackageRegistration };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User requestingOwner,
                User newOwner,
                PackageRegistration packageRegistration)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageOwnershipRequestDeclinedMessage(
                    configuration,
                    requestingOwner,
                    newOwner,
                    packageRegistration));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsOwnersWithPermissionToToList()
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

                var message = CreateMessage(
                    organization,
                    requestingOwnerEmailAllowed: true,
                    isRequestingOwnerOrganization: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(organizationAdmin.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void ReturnsEmailRecipientsNoneWhenRequestingOwnerEmailNotAllowed()
            {
                var message = CreateMessage(requestingOwnerEmailAllowed: false);
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
            public void AddsRequestingOwnerAddressToReplyToList()
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
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForUser, false)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForUser, false)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForUser, false)]
            [InlineData(EmailFormat.Markdown, _expectedMessageBodyForOrganization, true)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBodyForOrganization, true)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForOrganization, true)]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString, bool isRequestingOwnerOrganization)
            {
                var message = CreateMessage(isRequestingOwnerOrganization: isRequestingOwnerOrganization);

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

        private static PackageOwnershipRequestDeclinedMessage CreateMessage(
            Organization organization = null,
            bool requestingOwnerEmailAllowed = true,
            bool isRequestingOwnerOrganization = false)
        {
            if (isRequestingOwnerOrganization && organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }
            var requestingOwner = isRequestingOwnerOrganization ? organization : Fakes.RequestingUser;
            requestingOwner.EmailAllowed = requestingOwnerEmailAllowed;

            return new PackageOwnershipRequestDeclinedMessage(
                Configuration,
                requestingOwner,
                Fakes.OrganizationAdmin,
                Fakes.Package.PackageRegistration);
        }

        private const string _expectedMessageBodyForUser =
            @"The user 'organizationAdmin' has declined your request to add them as an owner of the package 'PackageId'.

Thanks,
The NuGetGallery Team";

        private const string _expectedMessageBodyForOrganization =
            @"The user 'organizationAdmin' has declined your organization's request to add them as an owner of the package 'PackageId'.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBodyForUser =
            "<p>The user 'organizationAdmin' has declined your request to add them as an owner of the package 'PackageId'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";

        private const string _expectedHtmlBodyForOrganization =
            "<p>The user 'organizationAdmin' has declined your organization's request to add them as an owner of the package 'PackageId'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
