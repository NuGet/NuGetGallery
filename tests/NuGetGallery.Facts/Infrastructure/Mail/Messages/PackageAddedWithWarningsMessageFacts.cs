// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageAddedWithWarningsMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.Package, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.WarningMessages };
                    yield return new object[] { Configuration, null, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.WarningMessages };
                    yield return new object[] { Configuration, Fakes.Package, null, Fakes.PackageSupportUrl, Fakes.WarningMessages };
                    yield return new object[] { Configuration, Fakes.Package, Fakes.PackageUrl, null, Fakes.WarningMessages };
                    yield return new object[] { Configuration, Fakes.Package, Fakes.PackageUrl, Fakes.PackageSupportUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Package package,
                string packageUrl,
                string packageSupportUrl,
                IEnumerable<string> warningMessages)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageAddedWithWarningsMessage(
                    configuration,
                    package,
                    packageUrl,
                    packageSupportUrl,
                    warningMessages));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsPackageOwnersSubscribedToPackagePushedNotificationToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(
                    Fakes.PackageOwnerSubscribedToPackagePushedNotification.ToMailAddress(),
                    recipients.To);
            }

            [Fact]
            public void DoesNotAddPackageOwnersNotSubscribedToPackagePushedNotificationToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.DoesNotContain(
                    Fakes.PackageOwnerNotSubscribedToPackagePushedNotification.ToMailAddress(),
                    recipients.To);
            }

            [Fact]
            public void HasEmptyCCList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.CC);
            }

            [Fact]
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
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
        public void SetsGalleryNoReplyAddressAsSender()
        {
            var message = CreateMessage();

            Assert.Equal(Configuration.GalleryNoReplyAddress, message.Sender);
        }

        private static PackageAddedWithWarningsMessage CreateMessage(IEnumerable<string> warningMessages = null)
        {
            return new PackageAddedWithWarningsMessage(
                Configuration,
                Fakes.Package,
                Fakes.PackageUrl,
                Fakes.PackageSupportUrl,
                warningMessages ?? Fakes.WarningMessages);
        }

        private const string _expectedMarkdownBody =
            @"The package [PackageId 1.0.0](packageUrl) was recently pushed to NuGetGallery by Username. If this was not intended, please [contact support](packageSupportUrl).

Warning message
";

        private const string _expectedPlainTextBody =
            @"The package PackageId 1.0.0 (packageUrl) was recently pushed to NuGetGallery by Username. If this was not intended, please contact support (packageSupportUrl).

Warning message";

        private const string _expectedHtmlBody =
            "<p>The package <a href=\"packageUrl\">PackageId 1.0.0</a> was recently pushed to NuGetGallery by Username. If this was not intended, please <a href=\"packageSupportUrl\">contact support</a>.</p>\n" +
"<p>Warning message</p>\n";
    }
}