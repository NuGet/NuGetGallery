// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ApiKeyRevokedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ApiKeyRevokedMessage(
            IMessageServiceConfiguration configuration,
            User user,
            CredentialTypeInfo credentialType)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
            CredentialType = credentialType ?? throw new ArgumentNullException(nameof(credentialType));
            if (!credentialType.IsApiKey) throw new ArgumentException("Provided credential is not an api key.");
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public CredentialTypeInfo CredentialType { get; }

        public User User { get; }

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[] { User.ToMailAddress() });

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_ApiKeyRevoked_Subject,
                _configuration.GalleryOwner.DisplayName,
                CredentialType.Description);

        protected override string GetMarkdownBody()
            => string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_ApiKeyRevoked_Body,
                CredentialType.Description);
    }
}
