// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationTransformCanceledMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _accountToTransform;
        private readonly User _accountToSendTo;
        private readonly User _accountToReplyTo;

        public OrganizationTransformCanceledMessage(
            ICoreMessageServiceConfiguration configuration,
            User accountToTransform,
            User accountToSendTo,
            User accountToReplyTo)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _accountToTransform = accountToTransform ?? throw new ArgumentNullException(nameof(accountToTransform));
            _accountToSendTo = accountToSendTo ?? throw new ArgumentNullException(nameof(accountToSendTo));
            _accountToReplyTo = accountToReplyTo ?? throw new ArgumentNullException(nameof(accountToReplyTo));
        }
        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[] { _accountToSendTo.ToMailAddress() },
                replyTo: new[] { _accountToReplyTo.ToMailAddress() });

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Transformation of account '{_accountToTransform.Username}' has been cancelled";

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"Transformation of account '{_accountToTransform.Username}' has been cancelled by user '{_accountToReplyTo.Username}'.

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}