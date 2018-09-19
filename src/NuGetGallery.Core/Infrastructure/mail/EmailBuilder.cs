// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail
{
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        protected abstract string GetPlainTextBody();
        protected abstract string GetMarkdownBody();

        public abstract IEmailRecipients GetRecipients();

        public abstract string GetSubject();
    }
}
