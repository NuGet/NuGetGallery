// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerAddedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public PackageOwnerAddedMessage(
            IMessageServiceConfiguration configuration,
            User toUser,
            User newOwner,
            PackageRegistration packageRegistration,
            string packageUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            PackageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            ToUser = toUser ?? throw new ArgumentNullException(nameof(toUser));
            NewOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User ToUser { get; }

        public User NewOwner { get; }

        public PackageRegistration PackageRegistration { get; }

        public string PackageUrl { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    ToUser,
                    ActionsRequiringPermissions.HandlePackageOwnershipRequest),
                replyTo: new[] { _configuration.GalleryNoReplyAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership update for '{PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return $@"User '{NewOwner.Username}' is now an owner of the package ['{PackageRegistration.Id}']({PackageUrl}).

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";
        }
    }
}