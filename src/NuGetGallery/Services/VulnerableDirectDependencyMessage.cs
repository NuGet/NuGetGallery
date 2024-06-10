// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class VulnerableDirectDependencyMessage : IValidationMessage
    {
        public VulnerableDirectDependencyMessage(string packageId, string versionSpec, string advisoryLink, string packageLink, string vulnerabilitySeverity)
        {
            PackageId = packageId;
            VersionSpec = versionSpec;
            AdvisoryLink = advisoryLink;
            PackageLink = packageLink;
            VulnerabilitySeverity = vulnerabilitySeverity;
        }

        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage => $"Package dependency {PackageId} {VersionSpec} has a known {VulnerabilitySeverity} severity vulnerability. {AdvisoryLink}";
        public string RawHtmlMessage => $"<a href=\"{PackageLink}\">{PackageId}</a> {VersionSpec}. {AdvisoryLink}";

        public string PackageId { get; set; }
        public string VersionSpec { get; set; }
        public string AdvisoryLink { get; set; }
        public string PackageLink { get; set; }
        public string VulnerabilitySeverity { get; set; }
    }
}