// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageSponsorshipManagementInput
    {
        [Display(Name = "Package IDs")]
        public string PackageIds { get; set; }

        [Display(Name = "Sponsorship URL")]
        public string SponsorshipUrl { get; set; }

        [Display(Name = "Action Reason")]
        public string Reason { get; set; }
    }

    public class PackageSponsorshipSearchResults
    {
        public PackageSponsorshipManagementInput Input { get; set; }
        public List<PackageSponsorshipResult> PackageResults { get; set; } = new List<PackageSponsorshipResult>();
    }

    public class PackageSponsorshipResult
    {
        public string PackageId { get; set; }
        public List<string> SponsorshipUrls { get; set; } = new List<string>();
        public List<string> Owners { get; set; } = new List<string>();
        
        // Additional properties for display
        public int TotalDownloads { get; set; }
        public bool IsLocked { get; set; }
        public bool IsVerified { get; set; }
    }
}
