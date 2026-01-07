// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class CredentialRevokedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly Credential _credential;
        private readonly string _leakedUrl;
        private readonly string _revocationSource;
        private readonly string _manageApiKeyUrl;
        private readonly string _contactUrl;

        public CredentialRevokedMessage(
            IMessageServiceConfiguration configuration,
            Credential credential,
            string leakedUrl,
            string revocationSource,
            string manageApiKeyUrl,
            string contactUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _leakedUrl = leakedUrl ?? throw new ArgumentNullException(nameof(leakedUrl));
            _revocationSource = revocationSource ?? throw new ArgumentNullException(nameof(revocationSource));
            _manageApiKeyUrl = manageApiKeyUrl ?? throw new ArgumentNullException(nameof(manageApiKeyUrl));
            _contactUrl = contactUrl ?? throw new ArgumentNullException(nameof(contactUrl));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { _credential.User.ToMailAddress() },
                cc: new[] { Sender });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] API key " +
            (_credential.Description != null ? "'" + _credential.Description + "' " : string.Empty) +
            "revoked due to a potential leak";

        protected override string GetMarkdownBody()
        {
            var body = @"Hi {0},

This is your friendly NuGet security bot! It appears that an API key {1}associated with your account was posted at {2}. As a precautionary measure, we have revoked this key to protect your account and packages. Please review your packages for any unauthorized activity.

Your key was found here: <{3}>

In the future, please be mindful of accidentally posting your API keys publicly!

You can regenerate this key or create a new one on the [Manage API Keys]({4}) page.

Here are the recommended ways to manage API keys:
- Save the API key into a local NuGet.Config using the [NuGet CLI](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use [environment variables](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.
- Use [GitHub encrypted secrets](https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets) to store and access API keys.

Need help? Reply to this email or [contact support]({5}).

Thanks,  
The {6} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _credential.User.Username,
                _credential.Description != null ? "'" + _credential.Description + "' " : string.Empty,
                _revocationSource,
                _leakedUrl,
                _manageApiKeyUrl,
                _contactUrl,
                _configuration.GalleryOwner.DisplayName);
        }
    }
}