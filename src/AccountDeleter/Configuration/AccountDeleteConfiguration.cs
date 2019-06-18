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
        public static string SourceSuccessSuffix = "Success";

        public string Thing1 { get; set; }

        public bool RespectEmailContactSetting { get; set; }

        public List<SourceConfiguration> SourceConfigurations { get; set; }

        public EmailConfiguration EmailConfiguration { get; set; }

        public Dictionary<string, string> TemplateReplacements { get; set; }

        public IEmailBuilder GetEmailBuilder(string source, bool success = false)
        {
            foreach (var sourceConfig in SourceConfigurations)
            {
                if (sourceConfig.SourceName == source)
                {
                    if (success)
                    {
                        if (sourceConfig.SendMessageOnSuccess)
                        {
                            return new AccountDeleteEmailBuilder(sourceConfig.SuccessSubjectTemplate, sourceConfig.SuccessMessageTemplate, EmailConfiguration.GalleryOwner);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return new AccountDeleteEmailBuilder(sourceConfig.SubjectTemplate, sourceConfig.MessageTemplate, EmailConfiguration.GalleryOwner);
                    }
                }
            }

            throw new UnknownSourceException();
        }
    }
}