// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Web;

namespace NuGetGallery.Infrastructure.Mail
{
    public abstract class ConfirmationEmailBuilder : EmailBuilder
    {
        protected readonly ICoreMessageServiceConfiguration Configuration;
        protected readonly User User;
        protected readonly string ConfirmationUrl;
        protected readonly string RawConfirmationUrl;
        protected readonly bool IsOrganization;

        protected ConfirmationEmailBuilder(
            ICoreMessageServiceConfiguration configuration,
            User user,
            string confirmationUrl)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
            IsOrganization = user is Organization;
            RawConfirmationUrl = confirmationUrl ?? throw new ArgumentNullException(nameof(confirmationUrl));
            ConfirmationUrl = HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_");
        }

        public override MailAddress Sender => Configuration.GalleryNoReplyAddress;
    }
}
