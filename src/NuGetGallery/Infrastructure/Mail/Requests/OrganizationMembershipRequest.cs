// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;

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

        public string ConfirmationUrl
        {
            get
            {
                if (ConfirmationUrl == null)
                {
                    return null;
                }

                return HttpUtility.UrlDecode(ConfirmationUrl).Replace("_", "\\_");
            }
        }
        public string RejectionUrl
        {
            get
            {
                if (RejectionUrl == null)
                {
                    return null;
                }

                return HttpUtility.UrlDecode(RejectionUrl).Replace("_", "\\_");
            }
        }
    }
}