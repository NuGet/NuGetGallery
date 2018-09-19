// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestDeclinedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Organization _organization;
        private readonly User _pendingUser;

        public OrganizationMembershipRequestDeclinedMessage(
            ICoreMessageServiceConfiguration configuration,
            Organization organization,
            User pendingUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _organization = organization ?? throw new ArgumentNullException(nameof(organization));
            _pendingUser = pendingUser ?? throw new ArgumentNullException(nameof(pendingUser));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _organization,
                ActionsRequiringPermissions.ManageAccount,
                replyTo: new[] { _pendingUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{_organization.Username}' declined";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_pendingUser.Username}' has declined your request to become a member of your organization.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}