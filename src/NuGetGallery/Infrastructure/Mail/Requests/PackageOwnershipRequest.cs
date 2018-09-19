// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Infrastructure.Mail.Requests
{
    public class PackageOwnershipRequest
    {
        public User FromUser { get; set; }
        public User ToUser { get; set; }
        public PackageRegistration PackageRegistration { get; set; }
        public string PackageUrl { get; set; }
        public string ConfirmationUrl { get; set; }
        public string RejectionUrl { get; set; }
        public string HtmlEncodedMessage { get; set; }
        public string PolicyMessage { get; set; }
    }
}