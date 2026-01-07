// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace NuGet.Services.Messaging.Email
{
    public class EmailRecipients : IEmailRecipients
    {
        public EmailRecipients(
            IReadOnlyList<MailAddress> to,
            IReadOnlyList<MailAddress> cc = null,
            IReadOnlyList<MailAddress> bcc = null,
            IReadOnlyList<MailAddress> replyTo = null)
        {
            To = to ?? throw new ArgumentNullException(nameof(to));
            CC = cc ?? new List<MailAddress>();
            Bcc = bcc ?? new List<MailAddress>();
            ReplyTo = replyTo ?? new List<MailAddress>();
        }

        public static IEmailRecipients None = new EmailRecipients(to: Array.Empty<MailAddress>());

        public IReadOnlyList<MailAddress> To { get; }

        public IReadOnlyList<MailAddress> CC { get; }

        public IReadOnlyList<MailAddress> Bcc { get; }

        public IReadOnlyList<MailAddress> ReplyTo { get; }
    }
}