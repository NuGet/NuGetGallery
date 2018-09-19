// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Requests
{
    public class ContactOwnersRequest
    {
        public MailAddress FromAddress { get; set; }
        public Package Package { get; set; }
        public string PackageUrl { get; set; }
        public string HtmlEncodedMessage { get; set; }
        public string EmailSettingsUrl { get; set; }
        public bool CopySender { get; set; }
    }
}