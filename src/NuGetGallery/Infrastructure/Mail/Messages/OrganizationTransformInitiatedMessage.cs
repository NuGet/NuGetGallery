// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformInitiatedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _accountToTransform;
        private readonly User _adminUser;
        private readonly string _rawCancellationUrl;
        private readonly string _cancellationUrl;

        public OrganizationTransformInitiatedMessage(
            IMessageServiceConfiguration configuration,
            User accountToTransform,
            User adminUser,
            string cancellationUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            _adminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));

            _rawCancellationUrl = cancellationUrl ?? throw new ArgumentNullException(nameof(cancellationUrl));
            _cancellationUrl = EscapeLinkForMarkdown(cancellationUrl);
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: _accountToTransform.EmailAllowed
                    ? new[] { _accountToTransform.ToMailAddress() }
                    : Array.Empty<MailAddress>(),
                replyTo: new[] { _adminUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Organization transformation for account '{_accountToTransform.Username}'";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"We have received a request to transform account '{_accountToTransform.Username}' into an organization with user '{_adminUser.Username}' as its admin.

To cancel the transformation:

[{_cancellationUrl}]({_rawCancellationUrl})

If you did not request this change, please contact support by responding to this email.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}