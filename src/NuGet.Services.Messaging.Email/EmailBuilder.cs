// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Web;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Abstract base class for building email messages.
    /// </summary>
    public abstract class EmailBuilder : IEmailBuilder
    {
        public abstract MailAddress Sender { get; }

        public string GetBody(EmailFormat format)
        {
            switch (format)
            {
                case EmailFormat.PlainText:
                    return GetPlainTextBody();
                case EmailFormat.Markdown:
                    return GetMarkdownBody();
                case EmailFormat.Html:
                    return GetHtmlBody();
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public abstract IEmailRecipients GetRecipients();

        public abstract string GetSubject();

        protected abstract string GetPlainTextBody();
        protected abstract string GetMarkdownBody();
        protected abstract string GetHtmlBody();

        /// <summary>
        /// Markdown sees the underscore as italics indicator, so underscores are stripped in the message.
        /// This prevents cut and pasting of the address or the use of text only email readers.
        /// </summary>
        /// <param name="encodedUrl">The encoded Url</param>
        /// <returns>Returns a Markdown-friendly url by escaping underscore characters.</returns>
        protected string EscapeLinkForMarkdown(string encodedUrl)
        {
            if (encodedUrl == null)
            {
                throw new ArgumentNullException(nameof(encodedUrl));
            }

            return HttpUtility.UrlDecode(encodedUrl).Replace("_", "\\_");
        }
    }
}