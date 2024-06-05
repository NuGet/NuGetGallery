// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformRequestMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _accountToTransform;
        private readonly User _adminUser;
        private readonly string _profileUrl;
        private readonly string _rawConfirmationUrl;
        private readonly string _confirmationUrl;
        private readonly string _rawRejectionUrl;
        private readonly string _rejectionUrl;

        public OrganizationTransformRequestMessage(
            IMessageServiceConfiguration configuration,
            User accountToTransform,
            User adminUser,
            string profileUrl,
            string confirmationUrl,
            string rejectionUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            _adminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));
            _profileUrl = profileUrl ?? throw new ArgumentNullException(nameof(profileUrl));

            _rawConfirmationUrl = confirmationUrl ?? throw new ArgumentNullException(nameof(confirmationUrl));
            _confirmationUrl = EscapeLinkForMarkdown(confirmationUrl);

            _rawRejectionUrl = rejectionUrl ?? throw new ArgumentNullException(nameof(rejectionUrl));
            _rejectionUrl = EscapeLinkForMarkdown(rejectionUrl);
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: _adminUser.EmailAllowed
                    ? new[] { _adminUser.ToMailAddress() }
                    : Array.Empty<MailAddress>(),
                replyTo: new[] { _accountToTransform.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Organization transformation for account '{_accountToTransform.Username}'";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"We have received a request to transform account ['{_accountToTransform.Username}']({_profileUrl}) into an organization.

To proceed with the transformation and become an administrator of '{_accountToTransform.Username}':

[{_confirmationUrl}]({_rawConfirmationUrl})

To cancel the transformation:

[{_rejectionUrl}]({_rawRejectionUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}