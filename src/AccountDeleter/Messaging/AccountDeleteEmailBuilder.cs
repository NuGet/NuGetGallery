// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;
using System;
using System.Net.Mail;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteEmailBuilder : IEmailBuilder
    {
        private readonly string _messageTemplate;
        private readonly string _subjectTemplate;

        public AccountDeleteEmailBuilder(string subjectTemplate, string messageTemplate, string sender)
        {
            _messageTemplate = messageTemplate ?? throw new ArgumentNullException(nameof(messageTemplate));
            _subjectTemplate = subjectTemplate ?? throw new ArgumentNullException(nameof(subjectTemplate));
            Sender = new MailAddress(sender);
        }

        public MailAddress Sender { get; }

        public string GetBody(EmailFormat format)
        {
            // dump message tempalte for now.
            return _messageTemplate;
        }

        public IEmailRecipients GetRecipients()
        {
            throw new NotImplementedException();
        }

        public string GetSubject()
        {
            return _subjectTemplate;
        }
    }
}
