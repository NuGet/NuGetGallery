// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class NewAccountMessage : ConfirmationEmailBuilder
    {
        public NewAccountMessage(
            IMessageServiceConfiguration configuration,
            User user,
            string confirmationUrl)
            : base(configuration, user, confirmationUrl)
        {
        }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { User.ToMailAddress() });
        }

        public override string GetSubject() => $"[{Configuration.GalleryOwner.DisplayName}] Please verify your account";

        protected override string GetMarkdownBody()
        {
            return $@"Thank you for {(IsOrganization ? "creating an organization on the" : "registering with the")} {Configuration.GalleryOwner.DisplayName}.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address using the following link:

[{ConfirmationUrl}]({RawConfirmationUrl})

Thanks,
The {Configuration.GalleryOwner.DisplayName} Team";
        }
    }
}
