// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformAcceptedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _accountToTransform;
        private readonly User _adminUser;

        public OrganizationTransformAcceptedMessage(
            IMessageServiceConfiguration configuration,
            User accountToTransform,
            User adminUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            _adminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));
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
            => $"[{_configuration.GalleryOwner.DisplayName}] Account '{_accountToTransform.Username}' has been transformed into an organization";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"Account '{_accountToTransform.Username}' has been transformed into an organization with user '{_adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}