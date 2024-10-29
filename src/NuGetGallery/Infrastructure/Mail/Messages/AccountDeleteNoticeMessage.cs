// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class AccountDeleteNoticeMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public AccountDeleteNoticeMessage(
            IMessageServiceConfiguration configuration,
            User user)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User User { get; }

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(to: new[] { User.ToMailAddress() });

        public override string GetSubject() => Strings.AccountDelete_SupportRequestTitle;

        protected override string GetMarkdownBody()
        {
            var body = @"We received a request to delete your account {0}. If you did not initiate this request, please contact the {1} team immediately.
{2}When your account will be deleted, we will:

- revoke your API key(s)
- remove you as the owner for any package you own
- remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of

We will not delete the NuGet packages associated with the account.

Thanks,
{2}The {1} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                User.Username,
                _configuration.GalleryOwner.DisplayName,
                Environment.NewLine);
        }
    }
}