// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberRemovedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Organization _organization;
        private readonly User _removedUser;

        public OrganizationMemberRemovedMessage(
            ICoreMessageServiceConfiguration configuration,
            Organization organization,
            User removedUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _organization = organization ?? throw new ArgumentNullException(nameof(organization));
            _removedUser = removedUser ?? throw new ArgumentNullException(nameof(removedUser));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[] { _organization.ToMailAddress() },
                replyTo: new[] { _removedUser.ToMailAddress() });

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership update for organization '{_organization.Username}'";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_removedUser.Username}' is no longer a member of organization '{_organization.Username}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}