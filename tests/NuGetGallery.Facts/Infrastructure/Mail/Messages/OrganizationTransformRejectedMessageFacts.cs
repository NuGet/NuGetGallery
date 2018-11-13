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
    public class OrganizationTransformRejectedMessageFacts : MarkdownMessageBuilderFacts
    {
        public class TheConstructor
        {
            public static IEnumerable<object[]> ConstructorArguments
            {
                get
                {
                    yield return new object[] { null, Fakes.RequestingUser, Fakes.OrganizationAdmin, It.IsAny<bool>() };
                    yield return new object[] { Configuration, null, Fakes.OrganizationAdmin, It.IsAny<bool>() };
                    yield return new object[] { Configuration, Fakes.RequestingUser, null, It.IsAny<bool>() };
                }
            }

            [Theory]
            [MemberData(nameof(ConstructorArguments))]
            public void GivenANullArgument_ItShouldThrow(
                IMessageServiceConfiguration configuration,
                User accountToTransform,
                User adminUser,
                bool isCanceledByAdmin)
            {
                Assert.Throws<ArgumentNullException>(() => new OrganizationTransformRejectedMessage(
                    configuration,
                    accountToTransform,
                    adminUser,
                    isCanceledByAdmin));
            }
        }

        public class TheGetRecipientsMethod
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void AddsAccountToSendToToListWhenAccountToSendToEmailAllowed(bool isCanceledByAdminUser)
            {
                var accountToSendTo = isCanceledByAdminUser
                    ? Fakes.RequestingUser 
                    : Fakes.OrganizationAdmin;

                var message = CreateMessage(
                    accountToTransformEmailAllowed: true, 
                    isCanceledByAdmin: isCanceledByAdminUser);

                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.To.Count);
                Assert.Contains(accountToSendTo.ToMailAddress(), recipients.To);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ReturnsRecipientsNoneWhenAccountToTransformEmailNotAllowed(bool isCanceledByAdminUser)
            {
                var message = CreateMessage(
                    accountToTransformEmailAllowed: false, 
                    isCanceledByAdmin: isCanceledByAdminUser);

                var recipients = message.GetRecipients();

                Assert.Empty(recipients.To);
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

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void AddsAccountToReplyToToReplyToList(bool isCanceledByAdminUser)
            {
                var accountToReplyTo = isCanceledByAdminUser
                    ? Fakes.OrganizationAdmin
                    : Fakes.RequestingUser;

                var message = CreateMessage(isCanceledByAdmin: isCanceledByAdminUser);
                var recipients = message.GetRecipients();

                Assert.Equal(1, recipients.ReplyTo.Count);
                Assert.Contains(accountToReplyTo.ToMailAddress(), recipients.ReplyTo);
            }
        }

        public class TheGetBodyMethod
        {
            [Theory]
            [InlineData(EmailFormat.Markdown, _expectedMessageBody)]
            [InlineData(EmailFormat.PlainText, _expectedMessageBody)]
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

        private static OrganizationTransformRejectedMessage CreateMessage(
            bool accountToTransformEmailAllowed = true,
            bool isCanceledByAdmin = false)
        {
            var accountToTransform = Fakes.RequestingUser;
            accountToTransform.EmailAllowed = isCanceledByAdmin && accountToTransformEmailAllowed;

            var adminUser = Fakes.OrganizationAdmin;
            adminUser.EmailAllowed = !isCanceledByAdmin && accountToTransformEmailAllowed;

            return new OrganizationTransformRejectedMessage(
                Configuration,
                accountToTransform,
                adminUser,
                isCanceledByAdmin);
        }

        private const string _expectedMessageBody =
            @"Transformation of account 'requestingUser' has been cancelled by user 'requestingUser'.

Thanks,
The NuGetGallery Team";

        private const string _expectedHtmlBody =
            "<p>Transformation of account 'requestingUser' has been cancelled by user 'requestingUser'.</p>\n" +
"<p>Thanks,\n" +
"The NuGetGallery Team</p>\n";
    }
}
