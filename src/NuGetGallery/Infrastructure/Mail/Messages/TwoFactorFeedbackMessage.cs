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
            MailAddress[] replyTo = new[] { new MailAddress(GetUserCommunicationEmailAddress().Trim()) };

            return new EmailRecipients(
                new[] { _configuration.GalleryOwner },
                cc: null,
                bcc: null,
                replyTo: replyTo);
        }

        public override string GetSubject() => $"[{_configuration.GalleryOwner.DisplayName}] Two-factor Authentication not enabled Feedback";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"**NuGet User:** {0} ({1})

This user chose **not** to enable the two-factor authentication!

**Feedback:**
{2}",
                _feedbackProvider.Username,
                GetUserCommunicationEmailAddress(),
                _feedbackText);
        }

        private string GetUserCommunicationEmailAddress()
        {
            return !string.IsNullOrEmpty(_feedbackProvider.EmailAddress) 
                ? _feedbackProvider.EmailAddress 
                : _feedbackProvider.UnconfirmedEmailAddress;
        }
    }
}