// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Requests;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ReportAbuseMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, new ReportPackageRequest(), It.IsAny<bool>() };
                    yield return new object[] { Configuration, null, It.IsAny<bool>() };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                ReportPackageRequest request,
                bool alreadyContactedOwners)
            {
                Assert.Throws<ArgumentNullException>(() => new ReportAbuseMessage(
                    configuration,
                    request,
                    alreadyContactedOwners));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsGalleryOwnerToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Single(recipients.To);
                Assert.Contains(Configuration.GalleryOwner, recipients.To);
            }

            [Fact]
            public void WhenCopySenderFalse_HasEmptyCCList()
            {
                var message = CreateMessage(copySender: false);
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void WhenCopySenderTrue_AddsFromAddressToCCList()
            {
                var message = CreateMessage(copySender: true);
                var recipients = message.GetRecipients();

                Assert.Single(recipients.CC);
                Assert.Contains(Fakes.FromAddress, recipients.CC);
            }

            [Fact]
            public void HasEmptyBccList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.Bcc);
            }

            [Fact]
            public void AddsFromAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Single(recipients.ReplyTo);
                Assert.Contains(Fakes.FromAddress, recipients.ReplyTo);
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
        public void SetsGalleryOwnerAsSender()
        {
            var message = CreateMessage();

            Assert.Equal(Configuration.GalleryOwner, message.Sender);
        }

        private static ReportAbuseMessage CreateMessage(
            bool copySender = false,
            bool alreadyContactedOwners = false)
        {
            var request = new ReportPackageRequest
            {
                FromAddress = Fakes.FromAddress,
                Package = Fakes.Package,
                PackageUrl = Fakes.PackageUrl,
                PackageVersionUrl = Fakes.PackageVersionUrl,
                Reason = "reason",
                Message = "message",
                RequestingUser = Fakes.RequestingUser,
                RequestingUserUrl = Fakes.ProfileUrl,
                Signature = "signature",
                CopySender = copySender
            };

            return new ReportAbuseMessage(
                Configuration,
                request,
                alreadyContactedOwners);
        }

        private const string _expectedMarkdownBody =
            @"**Email:** Sender (sender@gallery\.org)

**Signature:** signature

**Package:** [PackageId](packageUrl)

**Version:** [1.0.0](packageVersionUrl)

**User:** [requestingUser (requestUser@gallery\.org)](profileUrl)

**Reason:** reason

**Has the package owner been contacted?** No

**Message:** message

_Message sent from NuGetGallery_";

        private const string _expectedPlainTextBody =
            @"Email: Sender (sender@gallery\.org)

Signature: signature

Package: PackageId (packageUrl)

Version: 1.0.0 (packageVersionUrl)

User: requestingUser (requestUser@gallery\.org) (profileUrl)

Reason: reason

Has the package owner been contacted? No

Message: message

Message sent from NuGetGallery";

        private const string _expectedHtmlBody =
            "<p><strong>Email:</strong> Sender (sender@gallery.org)</p>\n" +
"<p><strong>Signature:</strong> signature</p>\n" +
"<p><strong>Package:</strong> <a href=\"packageUrl\">PackageId</a></p>\n" +
"<p><strong>Version:</strong> <a href=\"packageVersionUrl\">1.0.0</a></p>\n" +
"<p><strong>User:</strong> <a href=\"profileUrl\">requestingUser (requestUser@gallery.org)</a></p>\n" +
"<p><strong>Reason:</strong> reason</p>\n" +
"<p><strong>Has the package owner been contacted?</strong> No</p>\n" +
"<p><strong>Message:</strong> message</p>\n" +
"<p><em>Message sent from NuGetGallery</em></p>\n";
    }
}
