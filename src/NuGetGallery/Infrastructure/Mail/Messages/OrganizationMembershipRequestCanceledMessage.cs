// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestCanceledMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMembershipRequestCanceledMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            User pendingUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            PendingUser = pendingUser ?? throw new ArgumentNullException(nameof(pendingUser));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User PendingUser { get; }

        public Organization Organization { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: PendingUser.EmailAllowed
                    ? new[] { PendingUser.ToMailAddress() }
                    : Array.Empty<MailAddress>(),
                replyTo: new[] { Organization.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{Organization.Username}' cancelled";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                   CultureInfo.CurrentCulture,
                   $@"The request for you to become a member of '{Organization.Username}' has been cancelled.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}