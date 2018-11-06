// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactOwnersMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.FromAddress, Fakes.Package, Fakes.PackageUrl, "message", Fakes.EmailSettingsUrl, It.IsAny<bool>() };
                    yield return new object[] { Configuration, null, Fakes.Package, Fakes.PackageUrl, "message", Fakes.EmailSettingsUrl, It.IsAny<bool>() };
                    yield return new object[] { Configuration, Fakes.FromAddress, null, Fakes.PackageUrl, "message", Fakes.EmailSettingsUrl, It.IsAny<bool>() };
                    yield return new object[] { Configuration, Fakes.FromAddress, Fakes.Package, null, "message", Fakes.EmailSettingsUrl, It.IsAny<bool>() };
                    yield return new object[] { Configuration, Fakes.FromAddress, Fakes.Package, Fakes.PackageUrl, null, Fakes.EmailSettingsUrl, It.IsAny<bool>() };
                    yield return new object[] { Configuration, Fakes.FromAddress, Fakes.Package, Fakes.PackageUrl, "message", null, It.IsAny<bool>() };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                MailAddress fromAddress,
                Package package,
                string packageUrl,
                string message,
                string emailSettingsUrl,
                bool copySender)
            {
                Assert.Throws<ArgumentNullException>(() => new ContactOwnersMessage(
                    configuration,
                    fromAddress,
                    package,
                    packageUrl,
                    message,
                    emailSettingsUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsPackageOwnersRequiringEmailAllowedToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(Fakes.PackageOwnerWithEmailAllowed.ToMailAddress(), recipients.To);
                Assert.DoesNotContain(Fakes.PackageOwnerWithEmailNotAllowed.ToMailAddress(), recipients.To);
            }

            [Fact]
            public void AddsFromAddressToReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(Fakes.FromAddress, recipients.ReplyTo);
            }

            [Fact]
            public void DoesNotAddFromAddressToCCList()
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

        private static ContactOwnersMessage CreateMessage()
        {
            return new ContactOwnersMessage(
                Configuration,
                Fakes.FromAddress,
                Fakes.Package,
                Fakes.PackageUrl,
                "user input",
                Fakes.EmailSettingsUrl);
        }

        private const string _expectedMarkdownBody =
            @"_User Sender &lt;sender@gallery.org&gt; sends the following message to the owners of Package '[PackageId 1.0.0](packageUrl)'._

user input

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the NuGetGallery and
    [change your email notification settings](emailSettingsUrl).
</em>";

        private const string _expectedPlainTextBody =
    @"User Sender &lt;sender@gallery.org&gt; sends the following message to the owners of Package 'PackageId 1.0.0 (packageUrl)'.

user input

-----------------------------------------------
    To stop receiving contact emails as an owner of this package, sign in to the NuGetGallery and
    change your email notification settings (emailSettingsUrl).";

        private const string _expectedHtmlBody =
            "<p><em>User Sender &lt;sender@gallery.org&gt; sends the following message to the owners of Package '<a href=\"packageUrl\">PackageId 1.0.0</a>'.</em></p>\n" +
"<p>user input</p>\n" +
@"<hr />
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the NuGetGallery and
    <a href=""emailSettingsUrl"">change your email notification settings</a>.
</em>";
    }
}
