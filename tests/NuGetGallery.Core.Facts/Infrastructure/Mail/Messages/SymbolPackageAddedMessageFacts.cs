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
    public class SymbolPackageAddedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.SymbolPackage, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.EmailSettingsUrl, It.IsAny<IEnumerable<string>>() };
                    yield return new object[] { Configuration, null, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.EmailSettingsUrl, It.IsAny<IEnumerable<string>>() };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, null, Fakes.PackageSupportUrl, Fakes.EmailSettingsUrl, It.IsAny<IEnumerable<string>>() };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageUrl, null, Fakes.EmailSettingsUrl, It.IsAny<IEnumerable<string>>() };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageUrl, Fakes.PackageSupportUrl, null, It.IsAny<IEnumerable<string>>() };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                SymbolPackage symbolPackage,
                string packageUrl,
                string packageSupportUrl,
                string emailSettingsUrl,
                IEnumerable<string> warningMessages)
            {
                Assert.Throws<ArgumentNullException>(() => new SymbolPackageAddedMessage(
                    configuration,
                    symbolPackage,
                    packageUrl,
                    packageSupportUrl,
                    emailSettingsUrl,
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

        private static SymbolPackageAddedMessage CreateMessage(IEnumerable<string> warningMessages = null)
        {
            return new SymbolPackageAddedMessage(
                Configuration,
                Fakes.SymbolPackage,
                Fakes.PackageUrl,
                Fakes.PackageSupportUrl,
                Fakes.EmailSettingsUrl,
                warningMessages);
        }

        private const string _expectedMarkdownBody =
            @"The symbol package [PackageId 1.0.0](packageUrl) was recently published on NuGetGallery by Username. If this was not intended, please [contact support](packageSupportUrl).

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the NuGetGallery and
    [change your email notification settings](emailSettingsUrl).
</em>";

        private const string _expectedPlainTextBody =
            @"The symbol package PackageId 1.0.0 (packageUrl) was recently published on NuGetGallery by Username. If this was not intended, please contact support (packageSupportUrl).

-----------------------------------------------
    To stop receiving emails as an owner of this package, sign in to the NuGetGallery and
    change your email notification settings (emailSettingsUrl).";

        private const string _expectedHtmlBody =
            "<p>The symbol package <a href=\"packageUrl\">PackageId 1.0.0</a> was recently published on NuGetGallery by Username. If this was not intended, please <a href=\"packageSupportUrl\">contact support</a>.</p>\n" +
@"<hr />
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the NuGetGallery and
    <a href=""emailSettingsUrl"">change your email notification settings</a>.
</em>";
    }
}