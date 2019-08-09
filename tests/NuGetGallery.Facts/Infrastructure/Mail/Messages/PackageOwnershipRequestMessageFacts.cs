// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Framework;
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
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void AddsOwnersWithPermissionToToList(bool hasMessage)
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

                var message = CreateMessage(hasMessage, organization, isToUserOrganization: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(organizationAdmin.ToMailAddress(), recipients.To);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void HasEmptyCCList(bool hasMessage)
            {
                var message = CreateMessage(hasMessage);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void HasEmptyBccList(bool hasMessage)
            {
                var message = CreateMessage(hasMessage);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void AddsFromUserMailAddressToReplyToList(bool hasMessage)
            {
                var message = CreateMessage(hasMessage);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            public static IEnumerable<object[]> ReturnsExpectedBody_Data
            {
                get
                {
                    foreach (var hasMessage in new[] { false, true })
                    {
                        yield return MemberDataHelper.AsData(EmailFormat.Markdown, GetExpectedMarkdownMessageForUser(hasMessage), hasMessage, false);
                        yield return MemberDataHelper.AsData(EmailFormat.PlainText, GetExpectedPlainTextMessageForUser(hasMessage), hasMessage, false);
                        yield return MemberDataHelper.AsData(EmailFormat.Html, GetExpectedHtmlBodyForUser(hasMessage), hasMessage, false);
                        yield return MemberDataHelper.AsData(EmailFormat.Markdown, GetExpectedMarkdownMessageForOrganization(hasMessage), hasMessage, true);
                        yield return MemberDataHelper.AsData(EmailFormat.PlainText, GetExpectedPlainTextMessageForOrganization(hasMessage), hasMessage, true);
                        yield return MemberDataHelper.AsData(EmailFormat.Html, GetExpectedHtmlBodyForOrganization(hasMessage), hasMessage, true);
                    }
                }
            }

            [Theory]
            [MemberData(nameof(ReturnsExpectedBody_Data))]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString, bool hasMessage, bool isToUserOrganization)
            {
                var message = CreateMessage(hasMessage, isToUserOrganization: isToUserOrganization);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SetsGalleryNoReplyAddressAsSender(bool hasMessage)
        {
            var message = CreateMessage(hasMessage);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static PackageOwnershipRequestMessage CreateMessage(
            bool hasMessage,
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
                hasMessage ? "html encoded message" : null,
                "policy message");
        }

        private static string GetExpectedMarkdownMessageForUser(bool hasMessage)
        {
            return $@"The user 'requestingUser' would like to add you as an owner of the package ['PackageId'](packageUrl).

policy message
{ (hasMessage ? @"
The user 'requestingUser' added the following message for you:

'html encoded message'" : "") }

To accept this request and become a listed owner of the package:

[confirmationUrl](confirmationUrl)

To decline:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        }

        private static string GetExpectedPlainTextMessageForUser(bool hasMessage)
        {
            return $@"The user 'requestingUser' would like to add you as an owner of the package 'PackageId' (packageUrl).

policy message{ (hasMessage ? @"

The user 'requestingUser' added the following message for you:

'html encoded message'" : "") }

To accept this request and become a listed owner of the package:

confirmationUrl

To decline:

cancellationUrl

Thanks,
The NuGetGallery Team";
        }

        private static string GetExpectedMarkdownMessageForOrganization(bool hasMessage)
        {
            return $@"The user 'requestingUser' would like to add your organization as an owner of the package ['PackageId'](packageUrl).

policy message
{ (hasMessage ? @"
The user 'requestingUser' added the following message for you:

'html encoded message'" : "") }

To accept this request and make your organization a listed owner of the package:

[confirmationUrl](confirmationUrl)

To decline:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        }

        private static string GetExpectedPlainTextMessageForOrganization(bool hasMessage)
        {
            return $@"The user 'requestingUser' would like to add your organization as an owner of the package 'PackageId' (packageUrl).

policy message{ (hasMessage ? @"

The user 'requestingUser' added the following message for you:

'html encoded message'" : "") }

To accept this request and make your organization a listed owner of the package:

confirmationUrl

To decline:

cancellationUrl

Thanks,
The NuGetGallery Team";
        }

        private static string GetExpectedHtmlBodyForUser(bool hasMessage)
        {
            return "<p>The user 'requestingUser' would like to add you as an owner of the package <a href=\"packageUrl\">'PackageId'</a>.</p>\n" +
"<p>policy message</p>\n" +
(hasMessage ? ("<p>The user 'requestingUser' added the following message for you:</p>\n" +
"<p>'html encoded message'</p>\n") : "") +
"<p>To accept this request and become a listed owner of the package:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>To decline:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
        }

        private static string GetExpectedHtmlBodyForOrganization(bool hasMessage)
        {
            return "<p>The user 'requestingUser' would like to add your organization as an owner of the package <a href=\"packageUrl\">'PackageId'</a>.</p>\n" +
"<p>policy message</p>\n" +
(hasMessage ? ("<p>The user 'requestingUser' added the following message for you:</p>\n" +
"<p>'html encoded message'</p>\n") : "") +
"<p>To accept this request and make your organization a listed owner of the package:</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>To decline:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
        }
    }
}
