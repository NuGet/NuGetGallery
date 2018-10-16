// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, null, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, null, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, null, Fakes.CancellationUrl, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, null, "message", "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, null, "policy message" };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.Package.PackageRegistration, Fakes.PackageUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl, "message", null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User fromUser,
                User toUser,
                PackageRegistration packageRegistration,
                string packageUrl,
                string confirmationUrl,
                string rejectionUrl,
                string htmlEncodedMessage,
                string policyMessage)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageOwnershipRequestMessage(
                    configuration,
                    fromUser,
                    toUser,
                    packageRegistration,
                    packageUrl,
                    confirmationUrl,
                    rejectionUrl,
                    htmlEncodedMessage,
                    policyMessage));
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

                var message = CreateMessage(organization, isToUserOrganization: true);
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
            public void AddsFromUserMailAddressToReplyToList()
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
            [InlineData(EmailFormat.Markdown, _expectedMarkdownMessageForUser, false)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextMessageForUser, false)]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownMessageForOrganization, true)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextMessageForOrganization, true)]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString, bool isToUserOrganization)
            {
                var message = CreateMessage(isToUserOrganization: isToUserOrganization);

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

        private static PackageOwnershipRequestMessage CreateMessage(
            Organization organization = null,
            bool isToUserOrganization = false)
        {
            if (organization == null)
            {
                organization = new Organization("requestingOrganization")
                {
                    EmailAddress = "requestOrganization@gallery.org",
                    EmailAllowed = true
                };
            }

            return new PackageOwnershipRequestMessage(
                Configuration,
                Fakes.RequestingUser,
                isToUserOrganization ? organization : Fakes.OrganizationAdmin,
                Fakes.Package.PackageRegistration,
                Fakes.PackageUrl,
                Fakes.ConfirmationUrl,
                Fakes.CancellationUrl,
                "html encoded message",
                "policy message");
        }

        private const string _expectedMarkdownMessageForUser =
            @"The user 'requestingUser' would like to add you as an owner of the package ['PackageId'](packageUrl).

policy message

The user 'requestingUser' added the following message for you:

'html encoded message'

To accept this request and become a listed owner of the package:

[confirmationUrl](confirmationUrl)

To decline:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextMessageForUser =
            @"The user 'requestingUser' would like to add you as an owner of the package 'PackageId' (packageUrl).

policy message

The user 'requestingUser' added the following message for you:

'html encoded message'

To accept this request and become a listed owner of the package:

confirmationUrl

To decline:

cancellationUrl

Thanks,
The NuGetGallery Team";

        private const string _expectedMarkdownMessageForOrganization =
            @"The user 'requestingUser' would like to add your organization as an owner of the package ['PackageId'](packageUrl).

policy message

The user 'requestingUser' added the following message for you:

'html encoded message'

To accept this request and make your organization a listed owner of the package:

[confirmationUrl](confirmationUrl)

To decline:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextMessageForOrganization =
            @"The user 'requestingUser' would like to add your organization as an owner of the package 'PackageId' (packageUrl).

policy message

The user 'requestingUser' added the following message for you:

'html encoded message'

To accept this request and make your organization a listed owner of the package:

confirmationUrl

To decline:

cancellationUrl

Thanks,
The NuGetGallery Team";
    }
}
