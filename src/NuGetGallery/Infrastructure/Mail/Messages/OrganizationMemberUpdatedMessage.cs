// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberUpdatedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMemberUpdatedMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            Membership membership)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            Membership = membership ?? throw new ArgumentNullException(nameof(membership));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Organization Organization { get; }

        public Membership Membership { get; }

        public override IEmailRecipients GetRecipients()
        {
            if (!Organization.EmailAllowed)
            {
                return EmailRecipients.None;
            }

            return new EmailRecipients(
                to: new[] { Organization.ToMailAddress() },
                replyTo: new[] { Membership.Member.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership update for organization '{Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            var membershipLevel = Membership.IsAdmin ? "an administrator" : "a collaborator";
            var member = Membership.Member;

            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{member.Username}' is now {membershipLevel} of organization '{Organization.Username}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}