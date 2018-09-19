// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerAddedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _toUser;
        private readonly User _newOwner;
        private readonly PackageRegistration _packageRegistration;
        private readonly string _packageUrl;

        public PackageOwnerAddedMessage(
            ICoreMessageServiceConfiguration configuration,
            User toUser,
            User newOwner,
            PackageRegistration packageRegistration,
            string packageUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _toUser = toUser ?? throw new ArgumentNullException(nameof(toUser));
            _newOwner = newOwner ?? throw new ArgumentNullException(nameof(newOwner));
            _packageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _toUser,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { _configuration.GalleryNoReplyAddress });
        }

        public override string GetSubject() 
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership update for '{_packageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return $@"User '{_newOwner.Username}' is now an owner of the package ['{_packageRegistration.Id}']({_packageUrl}).

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";
        }

        protected override string GetPlainTextBody()
        {
            return $@"User '{_newOwner.Username}' is now an owner of the package '{_packageRegistration.Id}' ({_packageUrl}).

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";
        }
    }
}