// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageAddedWithWarningsMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Package _package;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;
        private readonly IEnumerable<string> _warningMessages;

        public PackageAddedWithWarningsMessage(
            ICoreMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl,
            IEnumerable<string> warningMessages)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
            _warningMessages = warningMessages ?? throw new ArgumentNullException(nameof(warningMessages));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var to = AddOwnersSubscribedToPackagePushedNotification();
            return new EmailRecipients(to);
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package pushed with warnings - {_package.PackageRegistration.Id} {_package.Version}";

        protected override string GetMarkdownBody()
        {
            var warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);

            return $@"The package [{_package.PackageRegistration.Id} {_package.Version}]({_packageUrl}) was recently pushed to {_configuration.GalleryOwner.DisplayName} by {_package.User.Username}. If this was not intended, please [contact support]({_packageSupportUrl}).
{warningMessagesPlaceholder}
";
        }

        protected override string GetPlainTextBody()
        {
            var warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);

            return $@"The package {_package.PackageRegistration.Id} {_package.Version} ({_packageUrl}) was recently pushed to {_configuration.GalleryOwner.DisplayName} by {_package.User.Username}. If this was not intended, please contact support: {_packageSupportUrl}.
{warningMessagesPlaceholder}
";
        }

        private IReadOnlyList<MailAddress> AddOwnersSubscribedToPackagePushedNotification()
        {
            var recipients = new List<MailAddress>();
            foreach (var owner in _package.PackageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                recipients.Add(owner.ToMailAddress());
            }
            return recipients;
        }
    }
}
