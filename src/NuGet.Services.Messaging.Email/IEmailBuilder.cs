// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Represents an email message builder.
    /// </summary>
    public interface IEmailBuilder
    {
        /// <summary>
        /// The sender of the email message.
        /// </summary>
        MailAddress Sender { get; }

        /// <summary>
        /// Retrieve the email message body in the requested <paramref name="format"/>.
        /// </summary>
        /// <param name="format">The requested markup format for the email body.</param>
        string GetBody(EmailFormat format);

        /// <summary>
        /// Gets the email message subject.
        /// </summary>
        string GetSubject();

        /// <summary>
        /// Gets the email recipients.
        /// </summary>
        IEmailRecipients GetRecipients();
    }
}