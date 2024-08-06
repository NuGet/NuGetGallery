// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery
{
    public enum ReportPackageReason
    {
        [Description("Other")]
        Other,

        [Description("A bug/failed to install")]
        HasABugOrFailedToInstall,

        [Description("Malicious code")]
        ContainsMaliciousCode,

        [Description("A security vulnerability")]
        ContainsSecurityVulnerability,

        [Description("Content infringing my copyright or trademark")]
        ViolatesALicenseIOwn,

        [Description("Private/confidential data")]
        ContainsPrivateAndConfidentialData,

        [Description("Content not intended to be published publicly on nuget.org")]
        ReleasedInPublicByAccident,

        [Description("Child sexual exploitation or abuse")]
        ChildSexualExploitationOrAbuse,

        [Description("Terrorism or violent extremism")]
        TerrorismOrViolentExtremism,

        [Description("Hate speech")]
        HateSpeech,

        [Description("Content related to imminent harm")]
        ImminentHarm,

        [Description("Non-consensual intimate imagery (i.e. \"revenge porn\")")]
        RevengePorn,

        [Description("Other nudity or pornography (not \"revenge porn\")")]
        OtherNudityOrPornography,
    }
}
