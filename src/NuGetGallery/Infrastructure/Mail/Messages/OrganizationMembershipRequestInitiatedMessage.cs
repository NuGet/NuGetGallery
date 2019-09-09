// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestInitiatedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMembershipRequestInitiatedMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            User requestingUser,
            User pendingUser,
            bool isAdmin)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            RequestingUser = requestingUser ?? throw new ArgumentNullException(nameof(requestingUser));
            PendingUser = pendingUser ?? throw new ArgumentNullException(nameof(pendingUser));
            IsAdmin = isAdmin;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Organization Organization { get; }

        public User RequestingUser { get; }

        public User PendingUser { get; }

        public bool IsAdmin { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    Organization,
                    ActionsRequiringPermissions.ManageAccount),
                replyTo: new[] { RequestingUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            var membershipLevel = IsAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{RequestingUser.Username}' has requested that user '{PendingUser.Username}' be added as {membershipLevel} of organization '{Organization.Username}'. A confirmation mail has been sent to user '{PendingUser.Username}' to accept the membership request. This mail is to inform you of the membership changes to organization '{Organization.Username}' and there is no action required from you.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}