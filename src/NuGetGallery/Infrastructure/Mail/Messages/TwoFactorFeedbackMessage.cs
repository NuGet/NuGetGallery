// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class TwoFactorFeedbackMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _feedbackText;
        private readonly User _feedbackProvider;

        public TwoFactorFeedbackMessage(
            IMessageServiceConfiguration configuration,
            string feedbackText,
            User feedbackProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _feedbackText = feedbackText ?? throw new ArgumentNullException(nameof(feedbackText));
            _feedbackProvider = feedbackProvider ?? throw new ArgumentNullException(nameof(feedbackProvider));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var replyTo = new[] { _feedbackProvider.ToMailAddress() };

            return new EmailRecipients(
                new[] { _configuration.GalleryOwner },
                cc: null,
                bcc: null,
                replyTo: replyTo);
        }

        public override string GetSubject() => $"[{_configuration.GalleryOwner.DisplayName}] 2FA not enabled feedback";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"**User:** {0}

This user chose **not** to enable the two-factor authentication!

**Feedback:**
{1}",
                _feedbackProvider.Username,
                _feedbackText);
        }
    }
}