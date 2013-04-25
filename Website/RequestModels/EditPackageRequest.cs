using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class EditPackageRequest
    {
        public EditPackageRequest()
        {
        }

        public EditPackageRequest(Package p)
        {
            DefaultTitle = p.PackageRegistration.DefaultTitle;
            Description = p.GetCurrentDescription();
            Summary = p.GetCurrentSummary();
            Tags = p.GetCurrentTags();
            IconUrl = p.GetCurrentIconUrl();
            ProjectUrl = p.GetCurrentProjectUrl();
            SourceCodeUrl = p.PackageRegistration.SourceCodeUrl;
            IssueTrackerUrl = p.PackageRegistration.IssueTrackerUrl;
        }

        [StringLength(256)]
        [Display(Name = "Default Package Title (if not specified by the package version)")]
        public string DefaultTitle { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        public string Summary { get; set; }

        [StringLength(256)]
        [Display(Name = "Icon URL")]
        [DataType(DataType.Url)]
        public string IconUrl { get; set; }

        [StringLength(256)]
        [Display(Name = "Project Homepage URL")]
        [DataType(DataType.Url)]
        public string ProjectUrl { get; set; }

        [StringLength(256)]
        [Display(Name = "Source Code URL (e.g. GitHub)")]
        [DataType(DataType.Url)]
        public string SourceCodeUrl { get; set; }

        [StringLength(256)]
        [DataType(DataType.Url)]
        [Display(Name = "Issue Tracker URL")]
        public string IssueTrackerUrl { get; set; }

        [StringLength(1024)]
        [DataType(DataType.Text)]
        public string Tags { get; set; }

        internal void UpdatePackageRegistration(PackageRegistration packageRegistration)
        {
            packageRegistration.DefaultTitle = DefaultTitle;
            packageRegistration.Description = Description;
            packageRegistration.Summary = Summary;
            packageRegistration.IconUrl = IconUrl;
            packageRegistration.ProjectUrl = ProjectUrl;
            packageRegistration.SourceCodeUrl = SourceCodeUrl;
            packageRegistration.IssueTrackerUrl = IssueTrackerUrl;
            packageRegistration.LastUpdated = DateTime.UtcNow;
        }
    }
}