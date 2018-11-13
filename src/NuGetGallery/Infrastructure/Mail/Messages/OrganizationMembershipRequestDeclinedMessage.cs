// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestDeclinedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMembershipRequestDeclinedMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            User pendingUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            PendingUser = pendingUser ?? throw new ArgumentNullException(nameof(pendingUser));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Organization Organization { get; }

        public User PendingUser { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    Organization,
                    ActionsRequiringPermissions.ManageAccount),
                replyTo: new[] { PendingUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{Organization.Username}' declined";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{PendingUser.Username}' has declined your request to become a member of your organization.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}