// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class CredentialRemovedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _user;
        private readonly string _credentialType;

        public CredentialRemovedMessage(
            ICoreMessageServiceConfiguration configuration,
            User user,
            string description,
            string credentialType)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _credentialType = credentialType ?? throw new ArgumentNullException(nameof(credentialType));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients() 
            => new EmailRecipients(
                to: new[] { _user.ToMailAddress() });

        public override string GetSubject() 
            => string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_CredentialRemoved_Subject,
                _configuration.GalleryOwner.DisplayName,
                _credentialType);

        protected override string GetMarkdownBody() => GetPlainTextBody();

        protected override string GetPlainTextBody() 
            => string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_CredentialRemoved_Body,
                _credentialType);
    }
}
