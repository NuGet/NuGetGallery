// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;
using NuGetGallery.AccountDeleter.Configuration;
using System.Collections.Generic;
using System.Net.Mail;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteConfiguration
    {
        public string Thing1 { get; set; }

        public bool RespectEmailContactSetting { get; set; }

        public string SenderEmail { get; set; }

        public List<SourceConfiguration> SourceConfigurations { get; set; }

        public IEmailBuilder GetEmailBuilder(string source)
        {
            foreach (var sourceConfig in SourceConfigurations)
            {
                if (sourceConfig.SourceName == source)
                {
                    return new AccountDeleteEmailBuilder(sourceConfig.SubjectTemplate, sourceConfig.MessageTemplate, SenderEmail);
                }
            }

            throw new UnknownSourceException();
        }
    }
}