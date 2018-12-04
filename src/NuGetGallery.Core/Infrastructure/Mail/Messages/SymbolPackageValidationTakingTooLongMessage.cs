// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SymbolPackageValidationTakingTooLongMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly SymbolPackage _symbolPackage;
        private readonly string _packageUrl;

        public SymbolPackageValidationTakingTooLongMessage(
            IMessageServiceConfiguration configuration,
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
            var to = EmailRecipientsHelper.GetOwnersSubscribedToPackagePushedNotification(_symbolPackage.Package.PackageRegistration);
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
            var body = @"It is taking longer than expected for your symbol package [{1} {2}]({3}) to get published.

We are looking into it and there is no action on you at this time. We’ll send you an email notification when your symbol package has been published.

Thank you for your patience.";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                _symbolPackage.Id,
                _symbolPackage.Version,
                _packageUrl);
        }
    }
}