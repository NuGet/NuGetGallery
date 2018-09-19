// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using System.Web;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestInitiatedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _requestingOwner;
        private readonly User _receivingOwner;
        private readonly User _newOwner;
        private readonly PackageRegistration _packageRegistration;
        private readonly string _rawCancellationUrl;
        private readonly string _cancellationUrl;

        public PackageOwnershipRequestInitiatedMessage(
            ICoreMessageServiceConfiguration configuration, 
            User requestingOwner,
            User receivingOwner,
            User newOwner,
            PackageRegistration packageRegistration,
            string cancellationUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _requestingOwner = requestingOwner ?? throw new ArgumentNullException(nameof(requestingOwner));
            _receivingOwner = receivingOwner ?? throw new ArgumentNullException(nameof(receivingOwner));
            _newOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            _packageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));

            _rawCancellationUrl = cancellationUrl ?? throw new ArgumentNullException(nameof(cancellationUrl));
            _cancellationUrl = HttpUtility.UrlDecode(cancellationUrl).Replace("_", "\\_");
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _receivingOwner,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { _newOwner.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{_packageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_requestingOwner.Username}' has requested that user '{_newOwner.Username}' be added as an owner of the package '{_packageRegistration.Id}'.

To cancel this request:

[{_cancellationUrl}]({_rawCancellationUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_requestingOwner.Username}' has requested that user '{_newOwner.Username}' be added as an owner of the package '{_packageRegistration.Id}'.

To cancel this request:
{_rawCancellationUrl}

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}
