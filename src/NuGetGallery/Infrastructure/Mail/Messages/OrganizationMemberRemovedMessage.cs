// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMemberRemovedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMemberRemovedMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            User removedUser)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            RemovedUser = removedUser ?? throw new ArgumentNullException(nameof(removedUser));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Organization Organization { get; }

        public User RemovedUser { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: Organization.EmailAllowed
                    ? new[] { Organization.ToMailAddress() }
                    : Array.Empty<MailAddress>(),
                replyTo: new[] { RemovedUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership update for organization '{Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{RemovedUser.Username}' is no longer a member of organization '{Organization.Username}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}