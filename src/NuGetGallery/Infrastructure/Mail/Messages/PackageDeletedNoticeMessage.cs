// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageDeletedNoticeMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Package _package;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;

        public PackageDeletedNoticeMessage(
            ICoreMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var to = AddAllOwnersToRecipients(_package.PackageRegistration);
            return new EmailRecipients(to);
        }

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Package deleted - {1} {2}",
                _configuration.GalleryOwner.DisplayName,
                _package.PackageRegistration.Id,
                _package.Version);

        protected override string GetMarkdownBody()
        {
            var body = @"The package [{1} {2}]({3}) was just deleted from {0}. If this was not intended, please [contact support]({4}).

Thanks,
The {0} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                _package.PackageRegistration.Id,
                _package.Version,
                _packageUrl,
                _packageSupportUrl);
        }

        protected override string GetPlainTextBody()
        {
            var body = @"The package {1} {2} ({3}) was just deleted from {0}. If this was not intended, please contact support: {4}.

Thanks,
The {0} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                _package.PackageRegistration.Id,
                _package.Version,
                _packageUrl,
                _packageSupportUrl);
        }

        private static IReadOnlyList<MailAddress> AddAllOwnersToRecipients(PackageRegistration packageRegistration)
        {
            var recipients = new List<MailAddress>();
            foreach (var owner in packageRegistration.Owners)
            {
                recipients.Add(owner.ToMailAddress());
            }
            return recipients;
        }
    }
}