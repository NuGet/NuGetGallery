// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestDeclinedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public PackageOwnershipRequestDeclinedMessage(
            IMessageServiceConfiguration configuration,
            User requestingOwner,
            User newOwner,
            PackageRegistration packageRegistration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            RequestingOwner = requestingOwner ?? throw new ArgumentNullException(nameof(requestingOwner));
            NewOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User RequestingOwner { get; }

        public User NewOwner { get; }

        public PackageRegistration PackageRegistration { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: RequestingOwner.EmailAllowed
                    ? GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                        RequestingOwner, ActionsRequiringPermissions.HandlePackageOwnershipRequest)
                    : Array.Empty<MailAddress>(),
                replyTo: new[] { NewOwner.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{PackageRegistration.Id}' declined";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{NewOwner.Username}' has declined {(RequestingOwner is Organization ? "your organization's" : "your")} request to add them as an owner of the package '{PackageRegistration.Id}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}
