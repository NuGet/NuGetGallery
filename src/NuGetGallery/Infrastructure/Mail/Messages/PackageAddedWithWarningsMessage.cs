// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageAddedWithWarningsMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly Package _package;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;
        private readonly IEnumerable<string> _warningMessages;

        public PackageAddedWithWarningsMessage(
            IMessageServiceConfiguration configuration,
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
            var to = EmailRecipientsHelper.GetOwnersSubscribedToPackagePushedNotification(_package.PackageRegistration);
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
    }
}