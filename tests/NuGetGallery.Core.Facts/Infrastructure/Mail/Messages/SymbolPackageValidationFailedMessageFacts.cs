// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Validation;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SymbolPackageValidationFailedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.SymbolPackage, Fakes.PackageValidationSet, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.AnnouncementsUrl, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, null, Fakes.PackageValidationSet, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.AnnouncementsUrl, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, null, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.AnnouncementsUrl, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageValidationSet, null, Fakes.PackageSupportUrl, Fakes.AnnouncementsUrl, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageValidationSet, Fakes.PackageUrl, null, Fakes.AnnouncementsUrl, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageValidationSet, Fakes.PackageUrl, Fakes.PackageSupportUrl, null, Fakes.TwitterUrl };
                    yield return new object[] { Configuration, Fakes.SymbolPackage, Fakes.PackageValidationSet, Fakes.PackageUrl, Fakes.PackageSupportUrl, Fakes.AnnouncementsUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                SymbolPackage symbolPackage,
                PackageValidationSet packageValidationSet,
                string packageUrl,
                string packageSupportUrl,
                string announcementsUrl,
                string twitterUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new SymbolPackageValidationFailedMessage(
                    configuration,
                    symbolPackage,
                    packageValidationSet,
                    packageUrl,
                    packageSupportUrl,
                    announcementsUrl,
                    twitterUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsOwnersToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(4, recipients.To.Count);
                Assert.Contains(
                    Fakes.PackageOwnerSubscribedToPackagePushedNotification.ToMailAddress(),
                    recipients.To);
                Assert.Contains(
                    Fakes.PackageOwnerNotSubscribedToPackagePushedNotification.ToMailAddress(),
                    recipients.To);
                Assert.Contains(
                    Fakes.PackageOwnerWithEmailAllowed.ToMailAddress(),
                    recipients.To);
                Assert.Contains(
                    Fakes.PackageOwnerWithEmailNotAllowed.ToMailAddress(),
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

        private static SymbolPackageValidationFailedMessage CreateMessage()
        {
            return new SymbolPackageValidationFailedMessage(
                Configuration,
                Fakes.SymbolPackage,
                Fakes.PackageValidationSet,
                Fakes.PackageUrl,
                Fakes.PackageSupportUrl,
                Fakes.AnnouncementsUrl,
                Fakes.TwitterUrl);
        }

        private const string _expectedMarkdownBody =
            @"The symbol package [PackageId 1.0.0](packageUrl) failed validation because of the following reason(s):

- There was an unknown failure when validating your package.

Your symbol package was not published on NuGetGallery and is not available for consumption.

Please [contact support](packageSupportUrl) to help.";

        private const string _expectedPlainTextBody =
    @"The symbol package PackageId 1.0.0 (packageUrl) failed validation because of the following reason(s):

- There was an unknown failure when validating your package.

Your symbol package was not published on NuGetGallery and is not available for consumption.

Please contact support (packageSupportUrl) to help.";

        private const string _expectedHtmlBody =
            "<p>The symbol package <a href=\"packageUrl\">PackageId 1.0.0</a> failed validation because of the following reason(s):</p>\n" +
"<ul>\n" +
"<li>There was an unknown failure when validating your package.</li>\n" +
"</ul>\n" +
"<p>Your symbol package was not published on NuGetGallery and is not available for consumption.</p>\n" +
"<p>Please <a href=\"packageSupportUrl\">contact support</a> to help.</p>\n";
    }
}