// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Messaging.Email;

namespace NuGet.SupportRequests.Notifications
{
    public class SupportRequestNotificationEmailBuilder : IEmailBuilder
    {
        public SupportRequestNotificationEmailBuilder(
            string subject,
            string htmlBody,
            string targetEmailAddress)
        {
            _subject = subject 
                ?? throw new ArgumentNullException(nameof(subject));
            _htmlBody = htmlBody 
                ?? throw new ArgumentNullException(nameof(htmlBody));
            _targetAddress = new MailAddress(
                targetEmailAddress ?? throw new ArgumentNullException(nameof(targetEmailAddress)));
        }

        public static MailAddress NoReplyAddress = new MailAddress("NuGet Gallery <noreply@nuget.org>");
        public MailAddress Sender => NoReplyAddress;

        private MailAddress _targetAddress;
        public IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(to: new[] { _targetAddress });
        }

        private readonly string _subject;
        public string GetSubject()
        {
            return _subject;
        }

        private readonly string _htmlBody;
        public string GetBody(EmailFormat format)
        {
            // We only create an HTML version of the body for now.
            // Ideally we would create a plaintext fallback as well, and return a different version of the body based on the format.
            // For now, however, returning an HTML body as the plaintext fallback is better than having a completely empty plaintext fallback.
            return _htmlBody;
        }
    }
}
