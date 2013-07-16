using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NuGetGallery
{
    public class EditPackageVersionRequest
    {
        public EditPackageVersionRequest()
        {
        }

        public EditPackageVersionRequest(Package package)
        {
            VersionTitle = package.Metadata.Title;
            IconUrl = package.Metadata.IconUrl;
            Summary = package.Metadata.Summary;
            Description = package.Metadata.Description;
            ProjectUrl = package.Metadata.ProjectUrl;
            Authors = package.Metadata.Authors;
            Copyright = package.Metadata.Copyright;
            Tags = package.Metadata.Tags;
            ReleaseNotes = package.Metadata.ReleaseNotes;
        }

        [StringLength(256)]
        [Display(Name = "Package Title")]
        [DataType(DataType.Text)]
        public string VersionTitle { get; set; } // not naming it Title in our model because that would clash with page Title in the property bag. Blech.

        [StringLength(256)]
        [Display(Name = "Icon URL")]
        [DataType(DataType.ImageUrl)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string IconUrl { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Summary")]
        [Subtext("Summary is a short text shown in package search results.")]
        public string Summary { get; set; }

        [DataType(DataType.MultilineText)]
        [Subtext("Description is a long text shown on the package page.")]
        public string Description { get; set; }

        [StringLength(256)]
        [Display(Name = "Project Homepage URL")]
        [DataType(DataType.Url)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string ProjectUrl { get; set; }

        [StringLength(512)]
        [Display(Name = "Authors - as a comma-separated list (ex. 'Annie, Bob, Charlie')")]
        [DataType(DataType.Text)]
        public string Authors { get; set; }

        [StringLength(512)]
        [Display(Name = "Copyright")]
        [DataType(DataType.Text)]
        public string Copyright { get; set; }

        [StringLength(1024)]
        [DataType(DataType.Text)]
        [Subtext("Tags - a space delimited list of keywords. (ex. 'CommandLine ASP.NET Proxies Scaffolding')")]
        public string Tags { get; set; }

        [Display(Name = "Release Notes (for this version)")]
        [DataType(DataType.MultilineText)]
        public string ReleaseNotes { get; set; }
    }
}
