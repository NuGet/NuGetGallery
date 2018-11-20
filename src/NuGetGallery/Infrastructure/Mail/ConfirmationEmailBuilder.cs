// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail
{
    public abstract class ConfirmationEmailBuilder : MarkdownEmailBuilder
    {
        protected readonly IMessageServiceConfiguration Configuration;
        protected readonly string RawConfirmationUrl;
        protected readonly bool IsOrganization;

        protected ConfirmationEmailBuilder(
            IMessageServiceConfiguration configuration,
            User user,
            string confirmationUrl)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
            IsOrganization = user is Organization;
            RawConfirmationUrl = confirmationUrl ?? throw new ArgumentNullException(nameof(confirmationUrl));
            ConfirmationUrl = EscapeLinkForMarkdown(confirmationUrl);
        }

        public override MailAddress Sender => Configuration.GalleryNoReplyAddress;

        public User User { get; }
        public string ConfirmationUrl { get; }
    }
}