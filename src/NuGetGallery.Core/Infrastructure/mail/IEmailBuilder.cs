// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail
{
    public interface IEmailBuilder
    {
        MailAddress Sender { get; }
        string GetBody(EmailFormat format);
        string GetSubject();
        IEmailRecipients GetRecipients();
    }
}
