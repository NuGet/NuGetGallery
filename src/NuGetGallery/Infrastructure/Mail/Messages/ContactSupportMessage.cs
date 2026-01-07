// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactSupportMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ContactSupportMessage(
            IMessageServiceConfiguration configuration,
            MailAddress fromAddress,
            User requestingUser,
            string message,
            string reason,
            bool copySender)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FromAddress = fromAddress ?? throw new ArgumentNullException(nameof(fromAddress));
            RequestingUser = requestingUser ?? throw new ArgumentNullException(nameof(requestingUser));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            CopySender = copySender;
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public MailAddress FromAddress { get; }
        public User RequestingUser { get; }
        public string Message { get; }
        public string Reason { get; }
        public bool CopySender { get; }

        public override IEmailRecipients GetRecipients()
                => new EmailRecipients(
                    to: new[] { _configuration.GalleryOwner },
                    cc: CopySender ? new[] { FromAddress } : null,
                    replyTo: new[] { FromAddress });

        public override string GetSubject() => $"Support Request (Reason: {Reason})";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                @"**Email:** {0} ({1})

**Reason:**
{2}

**Message:**
{3}",
                RequestingUser.Username,
                RequestingUser.EmailAddress,
                Reason,
                Message);
        }
    }
}