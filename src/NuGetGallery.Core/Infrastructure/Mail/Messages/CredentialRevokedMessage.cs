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
        private readonly string _siteRoot;
        private string _apiKeysAccountUrl => _siteRoot + "account/apikeys";
        private string _contactUrl => _siteRoot + "policies/Contact";

        public CredentialRevokedMessage(
            IMessageServiceConfiguration configuration,
            Credential credential,
            string leakedUrl,
            string revocationSource,
            string siteRoot)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _leakedUrl = leakedUrl ?? throw new ArgumentNullException(nameof(leakedUrl));
            _revocationSource = revocationSource ?? throw new ArgumentNullException(nameof(revocationSource));
            _siteRoot = siteRoot ?? throw new ArgumentNullException(nameof(siteRoot));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { _credential.User.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] API key " +
            (_credential.Description != null ? "'" + _credential.Description + "' " : string.Empty) +
            "revoked due to a potential leak";

        protected override string GetMarkdownBody()
        {
            var body = @"Hey {0},

This is your friendly NuGet security bot! It appears that an API key {1}linked with your account, was posted at {2}. As a precautionary measure, we have revoked this key to protect your account and packages.

Your key was found here: <{3}>

In the future, please be mindful of accidentally posting your API keys publicly!

Login to your NuGet.org account to regenerate this key or create a new one - [manage API keys]({4})

Here are the recommended ways to manage API keys:
- Save the API key into a local NuGet.Config using the [NuGet CLI](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-setapikey). This file should NOT be checked-in to version control or GitHub;
- Use [environment variables](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#setting-a-value) to set and access API keys.

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
                _apiKeysAccountUrl,
                _contactUrl,
                _configuration.GalleryOwner.DisplayName);
        }
    }
}