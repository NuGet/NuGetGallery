// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SymbolPackageValidationTakingTooLongMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly SymbolPackage _symbolPackage;
        private readonly string _packageUrl;

        public SymbolPackageValidationTakingTooLongMessage(
            ICoreMessageServiceConfiguration configuration,
            SymbolPackage symbolPackage,
            string packageUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _symbolPackage = symbolPackage ?? throw new ArgumentNullException(nameof(symbolPackage));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var to = AddOwnersSubscribedToPackagePushedNotification();
            return new EmailRecipients(to);
        }

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Symbol package validation taking longer than expected - {1} {2}",
                _configuration.GalleryOwner.DisplayName,
                _symbolPackage.Package.PackageRegistration.Id,
                _symbolPackage.Version);

        protected override string GetMarkdownBody()
        {
            string body = "It is taking longer than expected for your symbol package [{1} {2}]({3}) to get published.\n\n" +
                   "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published.\n\n" +
                   "Thank you for your patience.";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                _symbolPackage.Id,
                _symbolPackage.Version,
                _packageUrl);
        }

        protected override string GetPlainTextBody()
        {
            string body = "It is taking longer than expected for your symbol package {1} {2} ({3}) to get published.\n\n" +
                   "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published.\n\n" +
                   "Thank you for your patience.";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                _symbolPackage.Id,
                _symbolPackage.Version,
                _packageUrl);
        }

        private IReadOnlyList<MailAddress> AddOwnersSubscribedToPackagePushedNotification()
        {
            var recipients = new List<MailAddress>();
            foreach (var owner in _symbolPackage.Package.PackageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                recipients.Add(owner.ToMailAddress());
            }
            return recipients;
        }
    }
}
