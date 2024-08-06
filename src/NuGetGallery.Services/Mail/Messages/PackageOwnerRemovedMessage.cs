// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerRemovedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public PackageOwnerRemovedMessage(
            IMessageServiceConfiguration configuration,
            User fromUser,
            User toUser,
            PackageRegistration packageRegistration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FromUser = fromUser ?? throw new ArgumentNullException(nameof(fromUser));
            ToUser = toUser ?? throw new ArgumentNullException(nameof(toUser));
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User FromUser { get; }

        public User ToUser { get; }

        public PackageRegistration PackageRegistration { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    ToUser,
                    ActionsRequiringPermissions.HandlePackageOwnershipRequest),
                replyTo: new[] { FromUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership removal for '{PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return $@"The user '{FromUser.Username}' removed {(ToUser is Organization ? "your organization" : "you")} as an owner of the package '{PackageRegistration.Id}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";
        }
    }
}