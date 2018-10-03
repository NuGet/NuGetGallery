// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Infrastructure.Mail.Requests
{
    public class OrganizationMembershipRequest
    {
        public Organization Organization { get; set; }
        public User NewUser { get; set; }
        public User AdminUser { get; set; }
        public bool IsAdmin { get; set; }
        public string ProfileUrl { get; set; }
        public string RawConfirmationUrl { get; set; }
        public string RawRejectionUrl { get; set; }
    }
}