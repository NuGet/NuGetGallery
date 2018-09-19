// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail
{
    public interface IEmailRecipients
    {
        IReadOnlyList<MailAddress> To { get; }
        IReadOnlyList<MailAddress> CC { get; }
        IReadOnlyList<MailAddress> Bcc { get; }
        IReadOnlyList<MailAddress> ReplyTo { get; }
    }
}
