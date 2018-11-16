// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using Xunit;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageValidationTakingTooLongMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.Package, Fakes.PackageUrl };
                    yield return new object[] { Configuration, null, Fakes.PackageUrl };
                    yield return new object[] { Configuration, Fakes.Package, null };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                Package package,
                string packageUrl)
            {
                Assert.Throws<ArgumentNullException>(() => new PackageValidationTakingTooLongMessage(
                    configuration,
                    package,
                    packageUrl));
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

        private static PackageValidationTakingTooLongMessage CreateMessage()
        {
            return new PackageValidationTakingTooLongMessage(
                Configuration,
                Fakes.Package,
                Fakes.PackageUrl);
        }

        private const string _expectedMarkdownBody =
            @"It is taking longer than expected for your package [PackageId 1.0.0](packageUrl) to get published.

We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.

Thank you for your patience.";

        private const string _expectedPlainTextBody =
            @"It is taking longer than expected for your package PackageId 1.0.0 (packageUrl) to get published.

We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.

Thank you for your patience.";

        private const string _expectedHtmlBody =
            "<p>It is taking longer than expected for your package <a href=\"packageUrl\">PackageId 1.0.0</a> to get published.</p>\n" +
"<p>We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.</p>\n" +
"<p>Thank you for your patience.</p>\n";
    }
}