// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageAddedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly Package _package;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;
        private readonly string _emailSettingsUrl;
        private readonly IEnumerable<string> _warningMessages;
        private readonly bool _hasWarnings;

        public PackageAddedMessage(
            ICoreMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl,
            string emailSettingsUrl,
            IEnumerable<string> warningMessages)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
            _warningMessages = warningMessages;
            _hasWarnings = warningMessages != null && warningMessages.Any();
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var to = AddOwnersSubscribedToPackagePushedNotification();
            return new EmailRecipients(to);
        }

        public override string GetSubject()
        {
            if (_hasWarnings)
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published with warnings - {_package.PackageRegistration.Id} {_package.Version}";
            }
            else
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published - {_package.PackageRegistration.Id} {_package.Version}";
            }
        }

        protected override string GetMarkdownBody()
        {
            var warningMessagesPlaceholder = string.Empty;
            if (_hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);
            }

            return $@"The package [{_package.PackageRegistration.Id} {_package.Version}]({_packageUrl}) was recently published on {_configuration.GalleryOwner.DisplayName} by {_package.User.Username}. If this was not intended, please [contact support]({_packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {_configuration.GalleryOwner.DisplayName} and
    [change your email notification settings]({_emailSettingsUrl}).
</em>";
        }

        protected override string GetPlainTextBody()
        {
            var warningMessagesPlaceholder = string.Empty;
            if (_hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);
            }

            return $@"The package {_package.PackageRegistration.Id} {_package.Version} ({_packageUrl}) was recently published on {_configuration.GalleryOwner.DisplayName} by {_package.User.Username}. If this was not intended, please contact support: {_packageSupportUrl}.
{warningMessagesPlaceholder}

-----------------------------------------------
    To stop receiving emails as an owner of this package, sign in to the {_configuration.GalleryOwner.DisplayName} and
    change your email notification settings: {_emailSettingsUrl}";
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
