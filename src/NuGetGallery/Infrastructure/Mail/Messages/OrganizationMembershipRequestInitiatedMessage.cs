// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestInitiatedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Organization _organization;
        private readonly User _requestingUser;
        private readonly User _pendingUser;
        private readonly string _cancellationUrl;
        private readonly bool _isAdmin;

        public OrganizationMembershipRequestInitiatedMessage(
            ICoreMessageServiceConfiguration configuration,
            Organization organization,
            User requestingUser,
            User pendingUser,
            bool isAdmin,
            string cancellationUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _organization = organization ?? throw new ArgumentNullException(nameof(organization));
            _requestingUser = requestingUser ?? throw new ArgumentNullException(nameof(requestingUser));
            _pendingUser = pendingUser ?? throw new ArgumentNullException(nameof(pendingUser));
            _cancellationUrl = cancellationUrl ?? throw new ArgumentNullException(nameof(cancellationUrl));
            _isAdmin = isAdmin;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _organization,
                ActionsRequiringPermissions.ManageAccount,
                replyTo: new[] { _requestingUser.ToMailAddress() });
        }

        public override string GetSubject() 
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{_organization.Username}'";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            var membershipLevel = _isAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_requestingUser.Username}' has requested that user '{_pendingUser.Username}' be added as {membershipLevel} of organization '{_organization.Username}'. A confirmation mail has been sent to user '{_pendingUser.Username}' to accept the membership request. This mail is to inform you of the membership changes to organization '{_organization.Username}' and there is no action required from you.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}