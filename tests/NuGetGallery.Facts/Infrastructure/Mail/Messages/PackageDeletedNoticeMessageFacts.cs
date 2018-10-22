// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageDeletedNoticeMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.Package, Fakes.PackageUrl, Fakes.PackageSupportUrl };
                    yield return new object[] { Configuration, null, Fakes.PackageUrl, Fakes.PackageSupportUrl };
                    yield return new object[] { Configuration, Fakes.Package, null, Fakes.PackageSupportUrl };
                    yield return new object[] { Configuration, Fakes.Package, Fakes.PackageUrl, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Package package,
                string packageUrl,
                string packageSupportUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageDeletedNoticeMessage(
                    configuration,
                    package,
                    packageUrl,
                    packageSupportUrl));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Fact]
            public void AddsPackageOwnersToToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Equal(Fakes.Package.PackageRegistration.Owners.Count, recipients.To.Count);

                foreach (var owner in Fakes.Package.PackageRegistration.Owners)
                {
                    Assert.Contains(owner.ToMailAddress(), recipients.To);
                }
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
            public void HasEmptyReplyToList()
            {
                var message = CreateMessage();
                var recipients = message.GetRecipients();

                Assert.Empty(recipients.ReplyTo);
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

        private static PackageDeletedNoticeMessage CreateMessage()
        {
            return new PackageDeletedNoticeMessage(
                Configuration,
                Fakes.Package,
                Fakes.PackageUrl,
                Fakes.PackageSupportUrl);
        }

        private const string _expectedMarkdownBody =
            @"The package [PackageId 1.0.0](packageUrl) was just deleted from NuGetGallery. If this was not intended, please [contact support](packageSupportUrl).

Thanks,
The NuGetGallery Team";
        private const string _expectedPlainTextBody =
            @"The package PackageId 1.0.0 (packageUrl) was just deleted from NuGetGallery. If this was not intended, please contact support (packageSupportUrl).

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>The package <a href=\"packageUrl\">PackageId 1.0.0</a> was just deleted from NuGetGallery. If this was not intended, please <a href=\"packageSupportUrl\">contact support</a>.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
