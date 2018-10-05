// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactSupportMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ContactSupportMessage(
            IMessageServiceConfiguration configuration,
            ContactSupportRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public ContactSupportRequest Request { get; }

        public override IEmailRecipients GetRecipients() 
            => new EmailRecipients(
                to: new[] { _configuration.GalleryOwner },
                cc: Request.CopySender ? new[] { Request.FromAddress } : null,
                replyTo: new[] { Request.FromAddress });

        public override string GetSubject() => $"Support Request (Reason: {Request.SubjectLine})";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"**Email:** {0} ({1})

**Reason:**
{2}

**Message:**
{3}",
                Request.RequestingUser.Username,
                Request.RequestingUser.EmailAddress,
                Request.SubjectLine,
                Request.Message);
        }
    }
}
