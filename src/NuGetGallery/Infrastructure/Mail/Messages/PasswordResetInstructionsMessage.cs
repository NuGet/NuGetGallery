// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PasswordResetInstructionsMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _user;
        private readonly string _resetPasswordUrl;
        private readonly bool _forgotPassword;

        public PasswordResetInstructionsMessage(
            ICoreMessageServiceConfiguration configuration,
            User user,
            string resetPasswordUrl,
            bool forgotPassword)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _resetPasswordUrl = resetPasswordUrl ?? throw new ArgumentNullException(nameof(resetPasswordUrl));
            _forgotPassword = forgotPassword;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { _user.ToMailAddress() });
        }

        public override string GetSubject()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                _forgotPassword ? Strings.Emails_ForgotPassword_Subject : Strings.Emails_SetPassword_Subject,
                _configuration.GalleryOwner.DisplayName);
        }

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                _forgotPassword ? Strings.Emails_ForgotPassword_MarkdownBody : Strings.Emails_SetPassword_MarkdownBody,
                _resetPasswordUrl,
                _configuration.GalleryOwner.DisplayName);
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                _forgotPassword ? Strings.Emails_ForgotPassword_PlainTextBody : Strings.Emails_SetPassword_PlainTextBody,
                _resetPasswordUrl,
                _configuration.GalleryOwner.DisplayName);
        }
    }
}
