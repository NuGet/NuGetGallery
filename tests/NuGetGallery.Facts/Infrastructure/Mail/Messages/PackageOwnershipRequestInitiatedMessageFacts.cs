// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestInitiatedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.PackageOwnerWithEmailAllowed, Fakes.Package.PackageRegistration, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin, Fakes.PackageOwnerWithEmailAllowed, Fakes.Package.PackageRegistration, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null, Fakes.PackageOwnerWithEmailAllowed, Fakes.Package.PackageRegistration, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, null, Fakes.Package.PackageRegistration, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.PackageOwnerWithEmailAllowed, null, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.PackageOwnerWithEmailAllowed, Fakes.Package.PackageRegistration, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User requestingOwner,
                User receivingOwner,
                User newOwner,
                PackageRegistration packageRegistration,
                string cancellationUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageOwnershipRequestInitiatedMessage(
                    configuration,
                    requestingOwner,
                    receivingOwner,
                    newOwner,
                    packageRegistration,
                    cancellationUrl));
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

                var message = CreateMessage(organization, isReceivingOwnerOrganization: true);
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
            public void AddsNewOwnerAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.PackageOwnerWithEmailAllowed.ToMailAddress(), recipients.ReplyTo);
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

        private static PackageOwnershipRequestInitiatedMessage CreateMessage(
            Organization organization = null,
            bool isReceivingOwnerOrganization = false)
        {
            if (isReceivingOwnerOrganization && organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }

            return new PackageOwnershipRequestInitiatedMessage(
                Configuration,
                Fakes.RequestingUser,
                isReceivingOwnerOrganization ? organization : Fakes.OrganizationAdmin,
                Fakes.PackageOwnerWithEmailAllowed,
                Fakes.Package.PackageRegistration,
                Fakes.CancellationUrl);
        }

        private const string _expectedMarkdownBody =
            @"The user 'requestingUser' has requested that user 'emailAllowed' be added as an owner of the package 'PackageId'.

To cancel this request:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBody =
            @"The user 'requestingUser' has requested that user 'emailAllowed' be added as an owner of the package 'PackageId'.

To cancel this request:

cancellationUrl

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>The user 'requestingUser' has requested that user 'emailAllowed' be added as an owner of the package 'PackageId'.</p>\n" +
"<p>To cancel this request:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
