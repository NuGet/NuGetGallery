// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Represents a set of email recipients.
    /// </summary>
    public interface IEmailRecipients
    {
        /// <summary>
        /// The list of email addresses the email is to be sent to.
        /// </summary>
        IReadOnlyList<MailAddress> To { get; }

        /// <summary>
        /// The list of email addresses to be cc-ed.
        /// </summary>
        IReadOnlyList<MailAddress> CC { get; }

        /// <summary>
        /// The list of email addresses to be bcc-ed.
        /// </summary>
        IReadOnlyList<MailAddress> Bcc { get; }

        /// <summary>
        /// The list of email addresses to be replied to.
        /// </summary>
        IReadOnlyList<MailAddress> ReplyTo { get; }
    }
}