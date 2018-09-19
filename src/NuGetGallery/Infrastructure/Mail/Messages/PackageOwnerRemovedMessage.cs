// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnerRemovedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _fromUser;
        private readonly User _toUser;
        private readonly PackageRegistration _packageRegistration;

        public PackageOwnerRemovedMessage(
            ICoreMessageServiceConfiguration configuration,
            User fromUser,
            User toUser,
            PackageRegistration packageRegistration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _fromUser = fromUser ?? throw new ArgumentNullException(nameof(fromUser));
            _toUser = toUser ?? throw new ArgumentNullException(nameof(toUser));
            _packageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _toUser,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { _fromUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership removal for '{_packageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return $@"The user '{_fromUser.Username}' removed {(_toUser is Organization ? "your organization" : "you")} as an owner of the package '{_packageRegistration.Id}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";
        }
    }
}