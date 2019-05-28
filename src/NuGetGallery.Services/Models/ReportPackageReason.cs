// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery.Services.Models
{
    public enum ReportPackageReason
    {
        [Description("Other")]
        Other,

        [Description("The package has a bug/failed to install")]
        HasABugOrFailedToInstall,

        [Description("The package contains malicious code")]
        ContainsMaliciousCode,

        [Description("The package is infringing my copyright or trademark")]
        ViolatesALicenseIOwn,

        [Description("The package contains private/confidential data")]
        ContainsPrivateAndConfidentialData,

        [Description("The package was not intended to be published publicly on nuget.org")]
        ReleasedInPublicByAccident,
    }
}
