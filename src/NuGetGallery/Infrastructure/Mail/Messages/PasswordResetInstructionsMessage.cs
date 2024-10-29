// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PasswordResetInstructionsMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public PasswordResetInstructionsMessage(
            IMessageServiceConfiguration configuration,
            User user,
            string resetPasswordUrl,
            bool forgotPassword)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
            ResetPasswordUrl = resetPasswordUrl ?? throw new ArgumentNullException(nameof(resetPasswordUrl));
            ForgotPassword = forgotPassword;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User User { get; }

        public string ResetPasswordUrl { get; }

        public bool ForgotPassword { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { User.ToMailAddress() });
        }

        public override string GetSubject()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ForgotPassword ? Strings.Emails_ForgotPassword_Subject : Strings.Emails_SetPassword_Subject,
                _configuration.GalleryOwner.DisplayName);
        }

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ForgotPassword ? Strings.Emails_ForgotPassword_MarkdownBody : Strings.Emails_SetPassword_MarkdownBody,
                ResetPasswordUrl,
                _configuration.GalleryOwner.DisplayName);
        }
    }
}
