// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactSupportMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly ContactSupportRequest _request;

        public ContactSupportMessage(
            ICoreMessageServiceConfiguration configuration,
            ContactSupportRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients() 
            => new EmailRecipients(
                to: new[] { _configuration.GalleryOwner },
                cc: _request.CopySender ? new[] { _request.FromAddress } : null,
                replyTo: new[] { _request.FromAddress });

        public override string GetSubject() => $"Support Request (Reason: {_request.SubjectLine})";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"**Email:** {0} ({1})

**Reason:**
{2}

**Message:**
{3}",
                _request.RequestingUser.Username,
                _request.RequestingUser.EmailAddress,
                _request.SubjectLine,
                _request.Message);
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"Email: {0} ({1})

Reason:
{2}

Message:
{3}",
                _request.RequestingUser.Username,
                _request.RequestingUser.EmailAddress,
                _request.SubjectLine,
                _request.Message);
        }
    }
}
