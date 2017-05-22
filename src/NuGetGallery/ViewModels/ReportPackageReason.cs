// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery
{
    public enum ReportPackageReason
    {
        [Description("Other")]
        Other,

        [Description("The package has a bug/failed to install")]
        HasABugOrFailedToInstall,

        [Description("The package contains malicious code")]
        ContainsMaliciousCode,

        [Description("The package violates a copyright I own")]
        ViolatesALicenseIOwn,

        [Description("The package owner is fraudulently claiming authorship")]
        IsFraudulent,

        [Description("The package contains private/confidential data")]
        ContainsPrivateAndConfidentialData,

        [Description("The package was not intended to be published publicly on nuget.org")]
        ReleasedInPublicByAccident,
    }
}
