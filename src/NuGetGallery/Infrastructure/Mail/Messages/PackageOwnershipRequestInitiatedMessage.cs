// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestInitiatedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _rawManageUrl;
        private readonly string _manageUrl;

        public PackageOwnershipRequestInitiatedMessage(
            IMessageServiceConfiguration configuration,
            User requestingOwner,
            User receivingOwner,
            User newOwner,
            PackageRegistration packageRegistration,
            string manageUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _rawManageUrl = manageUrl ?? throw new ArgumentNullException(nameof(manageUrl));
            _manageUrl = EscapeLinkForMarkdown(manageUrl);

            RequestingOwner = requestingOwner ?? throw new ArgumentNullException(nameof(requestingOwner));
            ReceivingOwner = receivingOwner ?? throw new ArgumentNullException(nameof(receivingOwner));
            NewOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User RequestingOwner { get; }

        public User ReceivingOwner { get; }

        public User NewOwner { get; }

        public PackageRegistration PackageRegistration { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    ReceivingOwner,
                    ActionsRequiringPermissions.HandlePackageOwnershipRequest),
                replyTo: new[] { NewOwner.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{RequestingOwner.Username}' has requested that user '{NewOwner.Username}' be added as an owner of the package '{PackageRegistration.Id}'.

To cancel this request:

[{_manageUrl}]({_rawManageUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}
