// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageDeletedNoticeMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;

        public PackageDeletedNoticeMessage(
            IMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
            Package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Package Package { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: EmailRecipientsHelper.GetAllOwners(
                    Package.PackageRegistration,
                    requireEmailAllowed: false));
        }

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Package deleted - {1} {2}",
                _configuration.GalleryOwner.DisplayName,
                Package.PackageRegistration.Id,
                Package.Version);

        protected override string GetMarkdownBody()
        {
            var body = @"The package [{1} {2}]({3}) was just deleted from {0}. If this was not intended, please [contact support]({4}).

Thanks,
The {0} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                Package.PackageRegistration.Id,
                Package.Version,
                _packageUrl,
                _packageSupportUrl);
        }
    }
}