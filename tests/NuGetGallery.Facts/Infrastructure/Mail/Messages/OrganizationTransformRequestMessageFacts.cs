// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformRequestMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin, Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null, Fakes.ProfileUrl, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, null, Fakes.ConfirmationUrl, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.ProfileUrl, null, Fakes.CancellationUrl };
                    yield return new object[] { Configuration, Fakes.RequestingUser, Fakes.OrganizationAdmin, Fakes.ProfileUrl, Fakes.ConfirmationUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User accountToTransform,
                User adminUser,
                string profileUrl,
                string confirmationUrl,
                string rejectionUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationTransformRequestMessage(
                    configuration,
                    accountToTransform,
                    adminUser,
                    profileUrl,
                    confirmationUrl,
                    rejectionUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsAdminUserAddressToToListIfAdminUserEmailAllowed()
            {
                var message = CreateMessage(adminUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.OrganizationAdmin.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void ReturnsEmailRecipientsNoneWhenAdminUserEmailNotAllowed()
            {
                var message = CreateMessage(adminUserEmailAllowed: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage(adminUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage(adminUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void AddsAccountToTransformMailAddressToReplyToList()
            {
                var message = CreateMessage(adminUserEmailAllowed: true);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.RequestingUser.ToMailAddress(), recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMarkdownMessage)]
            [InlineData(EmailFormat.PlainText, _expectedPlainTextMessage)]
            [InlineData(EmailFormat.Html, _expectedHtmlBody)]
            public void ReturnsExpectedBody(EmailFormat format, string expectedString)
            {
                var message = CreateMessage(adminUserEmailAllowed: true);

                var body = message.GetBody(format);
                Assert.Equal(expectedString, body);
            }
        }

        [Fact]
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage(adminUserEmailAllowed: true);

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static OrganizationTransformRequestMessage CreateMessage(
            bool adminUserEmailAllowed)
        {
            var adminUser = Fakes.OrganizationAdmin;
            adminUser.EmailAllowed = adminUserEmailAllowed;

            return new OrganizationTransformRequestMessage(
                Configuration,
                Fakes.RequestingUser,
                adminUser,
                Fakes.ProfileUrl,
                Fakes.ConfirmationUrl,
                Fakes.CancellationUrl);
        }

        private const string _expectedMarkdownMessage =
            @"We have received a request to transform account ['requestingUser'](profileUrl) into an organization.

To proceed with the transformation and become an administrator of 'requestingUser':

[confirmationUrl](confirmationUrl)

To cancel the transformation:

[cancellationUrl](cancellationUrl)

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextMessage =
            @"We have received a request to transform account 'requestingUser' (profileUrl) into an organization.

To proceed with the transformation and become an administrator of 'requestingUser':

confirmationUrl

To cancel the transformation:

cancellationUrl

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>We have received a request to transform account <a href=\"profileUrl\">'requestingUser'</a> into an organization.</p>\n" +
"<p>To proceed with the transformation and become an administrator of 'requestingUser':</p>\n" +
"<p><a href=\"confirmationUrl\">confirmationUrl</a></p>\n" +
"<p>To cancel the transformation:</p>\n" +
"<p><a href=\"cancellationUrl\">cancellationUrl</a></p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
