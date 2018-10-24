// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Mail;
using NuGet.Services.Entities;

namespace NuGetGallery.Infrastructure.Mail.Requests
{
    public class ReportPackageRequest
    {
        public MailAddress FromAddress { get; set; }
        public User RequestingUser { get; set; }
        public Package Package { get; set; }
        public string Reason { get; set; }
        public string Signature { get; set; }
        public string Message { get; set; }
        public bool CopySender { get; set; }
        public string PackageUrl { get; set; }
        public string PackageVersionUrl { get; set; }
        public string RequestingUserUrl { get; set; }
    }
}