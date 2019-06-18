// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;
using System;
using System.Net.Mail;

namespace NuGetGallery.AccountDeleter.Messengers
{
    public class DisposableEmailBuilder : IEmailBuilder
    {
        private const string USERNAME_PLACEHOLDER = "USERNAME";

        private readonly IEmailBuilder _parentEmailBuilder;
        private readonly IEmailRecipients _emailRecipients;
        private readonly ITemplater _templater;

        public DisposableEmailBuilder(IEmailBuilder parentBuilder, IEmailRecipients emailRecipients, string username, ITemplater templater)
        {
            _parentEmailBuilder = parentBuilder ?? throw new ArgumentNullException(nameof(parentBuilder));
            _emailRecipients = emailRecipients ?? throw new ArgumentNullException(nameof(emailRecipients));
            _templater = templater ?? throw new ArgumentNullException(nameof(templater));

            _templater.AddReplacement(USERNAME_PLACEHOLDER, username);
        }

        public MailAddress Sender
        {
            get
            {
                return _parentEmailBuilder.Sender;
            }
        }

        public string GetBody(EmailFormat format)
        {
            // run through a replacer
            return _templater.FillTemplate(_parentEmailBuilder.GetBody(format));
        }

        public IEmailRecipients GetRecipients()
        {
            return _emailRecipients;
        }

        public string GetSubject()
        {
            // run through a replacer
            return _templater.FillTemplate(_parentEmailBuilder.GetSubject());
        }
    }
}
