// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using NuGet.Services.Entities;

namespace NuGet.Services.Messaging.Email
{
    public class PackageAddedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;
        private readonly string _emailSettingsUrl;
        private readonly IEnumerable<string> _warningMessages;
        private readonly bool _hasWarnings;

        public PackageAddedMessage(
            IMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl,
            string emailSettingsUrl,
            IEnumerable<string> warningMessages)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
            _warningMessages = warningMessages;
            _hasWarnings = warningMessages != null && warningMessages.Any();
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Package Package { get; }

        public override IEmailRecipients GetRecipients()
        {
            var to = EmailRecipients.GetOwnersSubscribedToPackagePushedNotification(Package.PackageRegistration);
            return new EmailRecipients(to);
        }

        public override string GetSubject()
        {
            if (_hasWarnings)
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published with warnings - {Package.PackageRegistration.Id} {Package.Version}";
            }
            else
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published - {Package.PackageRegistration.Id} {Package.Version}";
            }
        }

        protected override string GetMarkdownBody()
        {
            var warningMessagesPlaceholder = string.Empty;
            if (_hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);
            }

            return $@"The package [{Package.PackageRegistration.Id} {Package.Version}]({_packageUrl}) was recently published on {_configuration.GalleryOwner.DisplayName} by {Package.User.Username}. If this was not intended, please [contact support]({_packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {_configuration.GalleryOwner.DisplayName} and
    [change your email notification settings]({_emailSettingsUrl}).
</em>";
        }

        protected override string GetPlainTextBody()
        {
            // The HTML emphasis tag is not supported by the Plain Text renderer in Markdig.
            // Manually overriding this one.
            var warningMessagesPlaceholder = string.Empty;
            if (_hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);
            }

            return $@"The package {Package.PackageRegistration.Id} {Package.Version} ({_packageUrl}) was recently published on {_configuration.GalleryOwner.DisplayName} by {Package.User.Username}. If this was not intended, please contact support ({_packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
    To stop receiving emails as an owner of this package, sign in to the {_configuration.GalleryOwner.DisplayName} and
    change your email notification settings ({_emailSettingsUrl}).";
        }
    }
}
