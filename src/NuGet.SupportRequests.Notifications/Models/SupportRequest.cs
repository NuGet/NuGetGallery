// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SupportRequests.Notifications.Models
{
    internal class SupportRequest
    {
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string OwnerEmail { get; set; }
        public string Reason { get; set; }
        public int? PackageRegistrationKey { get; set; }
        public string AdminPagerDutyUsername { get; set; }
        public string AdminGalleryUsername { get; set; }
        public int IssueStatus { get; set; }
    }
}