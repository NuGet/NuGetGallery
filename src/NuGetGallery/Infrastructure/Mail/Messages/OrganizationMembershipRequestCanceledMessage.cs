// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestCanceledMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Organization _organization;
        private readonly User _pendingUser;

        public OrganizationMembershipRequestCanceledMessage(
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
            => new EmailRecipients(
                to: new[] { _pendingUser.ToMailAddress() },
                replyTo: new[] { _organization.ToMailAddress() });

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{_organization.Username}' cancelled";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                   CultureInfo.CurrentCulture,
                   $@"The request for you to become a member of '{_organization.Username}' has been cancelled.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}