// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery.Services
{
    public class ContactSupportRequest
    {
        public MailAddress FromAddress { get; set; }
        public User RequestingUser { get; set; }
        public string Message { get; set; }
        public string SubjectLine { get; set; }
        public bool CopySender { get; set; }
    }
}