// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestDeclinedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _requestingOwner;
        private readonly User _newOwner;
        private readonly PackageRegistration _packageRegistration;

        public PackageOwnershipRequestDeclinedMessage(
            ICoreMessageServiceConfiguration configuration,
            User requestingOwner,
            User newOwner,
            PackageRegistration packageRegistration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _requestingOwner = requestingOwner ?? throw new ArgumentNullException(nameof(requestingOwner));
            _newOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            _packageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _requestingOwner,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { _newOwner.ToMailAddress() });
        }

        public override string GetSubject() 
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{_packageRegistration.Id}' declined";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_newOwner.Username}' has declined {(_requestingOwner is Organization ? "your organization's" : "your")} request to add them as an owner of the package '{_packageRegistration.Id}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}
