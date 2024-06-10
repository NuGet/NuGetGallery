using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.ViewModels
{
    public class VulnerableDependencyModel
    {
        public VulnerableDependencyModel(string packageId, string versionSpec, string advisoryLink, string packageLink, string vulnerabilitySeverity)
        {
            PackageId = packageId;
            VersionSpec = versionSpec;
            AdvisoryLink = advisoryLink;
            PackageLink = packageLink;
            VulnerabilitySeverity = vulnerabilitySeverity;
        }

        public string PackageId { get; set; }
        public string VersionSpec { get; set; }
        public string AdvisoryLink { get; set; }
        public string PackageLink { get; set; }
        public string VulnerabilitySeverity { get; set; }
    }
}