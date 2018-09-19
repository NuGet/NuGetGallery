// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ApiKeyAddedMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly User _user;
        private readonly string _description;

        public ApiKeyAddedMessage(
            ICoreMessageServiceConfiguration configuration,
            User user,
            string description)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { _user.ToMailAddress() });
        }

        public override string GetSubject()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_CredentialAdded_Subject,
                _configuration.GalleryOwner.DisplayName,
                Strings.CredentialType_ApiKey);
        }

        protected override string GetMarkdownBody()
        {
            return GetPlainTextBody();
        }

        protected override string GetPlainTextBody()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_ApiKeyAdded_Body,
                _description);
        }
    }
}
