// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberUpdatedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Organization _organization;
        private readonly Membership _membership;

        public OrganizationMemberUpdatedMessage(
            ICoreMessageServiceConfiguration configuration,
            Organization organization,
            Membership membership)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _organization = organization ?? throw new ArgumentNullException(nameof(organization));
            _membership = membership ?? throw new ArgumentNullException(nameof(membership));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[] { _organization.ToMailAddress() },
                replyTo: new[] { _membership.Member.ToMailAddress() });

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership update for organization '{_organization.Username}'";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            var membershipLevel = _membership.IsAdmin ? "an administrator" : "a collaborator";
            var member = _membership.Member;

            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{member.Username}' is now {membershipLevel} of organization '{_organization.Username}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}