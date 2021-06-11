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

        [Description("The package is infringing my copyright or trademark")]
        ViolatesALicenseIOwn,

        [Description("The package contains private/confidential data")]
        ContainsPrivateAndConfidentialData,

        [Description("The package was not intended to be published publicly on nuget.org")]
        ReleasedInPublicByAccident,

        [Description("Child sexual exploitation or abuse")]
        ChildSexualExploitationOrAbuse,

        [Description("Terrorism or violent extremism")]
        TerrorismOrViolentExtremism,

        [Description("The package contains hate speech")]
        HateSpeech,

        [Description("The package contains content related to imminent harm")]
        ImminentHarm,

        [Description("The package contains non-consensual intimate imagery (i.e. \"revenge porn\")")]
        RevengePorn,

        [Description("Other nudity or pornography (not \"revenge porn\")")]
        OtherNudityOrPornography,
    }
}
