// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class EmailChangeConfirmationMessage : ConfirmationEmailBuilder
    {
        public EmailChangeConfirmationMessage(
            IMessageServiceConfiguration configuration,
            User newUser,
            string confirmationUrl)
            : base(configuration, newUser, confirmationUrl)
        {
        }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { new MailAddress(User.UnconfirmedEmailAddress, User.Username) });
        }

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Please verify your {1}'s new email address",
                Configuration.GalleryOwner.DisplayName,
                IsOrganization ? "organization" : "account");

        protected override string GetMarkdownBody()
        {
            var bodyTemplate = @"You recently changed your {0}'s {1} email address.

To verify {0} new email address:

[{2}]({3})

Thanks,
The {1} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                IsOrganization ? "organization" : "account",
                Configuration.GalleryOwner.DisplayName,
                ConfirmationUrl,
                RawConfirmationUrl);
        }
    }
}