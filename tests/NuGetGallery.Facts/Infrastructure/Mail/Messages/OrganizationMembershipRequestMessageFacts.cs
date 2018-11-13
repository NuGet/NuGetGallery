// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingOrganization, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>(), Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, null, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>(), Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, null, Fakes.OrganizationAdmin, It.IsAny<bool>(), Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, null, It.IsAny<bool>(), Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>(), null, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>(), Fakes.ProfileUrl, null, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingOrganization, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>(), Fakes.ProfileUrl, Fakes.ConfirmationUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Organization organization,
                User newUser,
                User adminUser,
                bool isAdmin,
                string profileUrl,
                string confirmationUrl,
                string rejectionUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationMembershipRequestMessage(
                    configuration,
                    organization,
                    newUser,
                    adminUser,
                    isAdmin,
                    profileUrl,
                    confirmationUrl,
                    rejectionUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsNewUserAddressToToListIfNewUserEmailAllowed()
            {
                var message = CreateMessage(newUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void ReturnsEmailRecipientsNoneWhenNewUserEmailNotAllowed()
            {
                var message = CreateMessage(newUserEmailAllowed: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(newUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(newUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void AddsOrganizationAndAdminMailAddressToReplyToList()
            {
                var message = CreateMessage(newUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(2, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.RequestingOrganization.ToMailAddress(), recipients.ReplyTo);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyForAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyForAdmin)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForAdmin)]
            public void ReturnsExpectedBodyForAdmin(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(newUserEmailAllowed: true, isAdmin: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }

            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownBodyForNonAdmin)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextBodyForNonAdmin)]
            [InlineData(EmailFormat.Html, _expectedHtmlBodyForNonAdmin)]
            public void ReturnsExpectedBodyForNonAdmin(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(newUserEmailAllowed: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage(newUserEmailAllowed: true);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static OrganizationMembershipRequestMessage CreateMessage(
            bool newUserEmailAllowed,
            bool isAdmin = false)
        {
            var newUser = Fakes.RequestingUser;
            newUser.EmailAllowed = newUserEmailAllowed;

            return new OrganizationMembershipRequestMessage(
                Configuration,
                Fakes.RequestingOrganization,
                Fakes.RequestingUser,
                newUser,
                isAdmin,
                Fakes.ProfileUrl,
                Fakes.ConfirmationUrl,
                Fakes.CancellationUrl);
        }

        private const string _expectedMarkdownBodyForAdmin =
            @"The user 'requestingUser' would like you to become an administrator of their organization, ['requestingOrganization'](profileUrl).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become an administrator of 'requestingOrganization':

[confirmationUrl](confirmationUrl)

To decline the request:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBodyForAdmin =
            @"The user 'requestingUser' would like you to become an administrator of their organization, 'requestingOrganization' (profileUrl).

To learn more about organization roles, refer to the documentation. (https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become an administrator of 'requestingOrganization':

confirmationUrl

To decline the request:

cancellationUrl

Thanks,
The NuGetGallery Team";
        private const string _expectedMarkdownBodyForNonAdmin =
            @"The user 'requestingUser' would like you to become a collaborator of their organization, ['requestingOrganization'](profileUrl).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become a collaborator of 'requestingOrganization':

[confirmationUrl](confirmationUrl)

To decline the request:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBodyForNonAdmin =
            @"The user 'requestingUser' would like you to become a collaborator of their organization, 'requestingOrganization' (profileUrl).

To learn more about organization roles, refer to the documentation. (https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become a collaborator of 'requestingOrganization':

confirmationUrl

To decline the request:

cancellationUrl

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBodyForAdmin =
            "<p>The user 'requestingUser' would like you to become an administrator of their organization, <a href=\"profileUrl\">'requestingOrganization'</a>.</p>\n" +
"<p>To learn more about organization roles, <a href=\"https://go.microsoft.com/fwlink/?linkid=870439\">refer to the documentation.</a></p>\n" +
"<p>To accept the request and become an administrator of 'requestingOrganization':</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>To decline the request:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";

        private const string _expectedHtmlBodyForNonAdmin =
            "<p>The user 'requestingUser' would like you to become a collaborator of their organization, <a href=\"profileUrl\">'requestingOrganization'</a>.</p>\n" +
"<p>To learn more about organization roles, <a href=\"https://go.microsoft.com/fwlink/?linkid=870439\">refer to the documentation.</a></p>\n" +
"<p>To accept the request and become a collaborator of 'requestingOrganization':</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>To decline the request:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
