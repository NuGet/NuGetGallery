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

        public EditPackageVersionRequest(Package package, PackageMetadata pendingMetadata)
        {
            var metadata = pendingMetadata ?? package.Metadata;
            VersionTitle = metadata.Title;
            IconUrl = metadata.IconUrl;
            Summary = metadata.Summary;
            Description = metadata.Description;
            ProjectUrl = metadata.ProjectUrl;
            Authors = metadata.Authors;
            Copyright = metadata.Copyright;
            Tags = metadata.Tags;
            ReleaseNotes = metadata.ReleaseNotes;
        }

        [StringLength(256)]
        [Display(Name = "Title")]
        [DataType(DataType.Text)]
        public string VersionTitle { get; set; } // not naming it Title in our model because that would clash with page Title in the property bag. Blech.

        [StringLength(256)]
        [Display(Name = "Icon URL")]
        [DataType(DataType.ImageUrl)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string IconUrl { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Summary (visible in search results)")]
        public string Summary { get; set; }

        [DataType(DataType.MultilineText)]
        [Display(Name = "Description (a longer description)")]
        public string Description { get; set; }

        [StringLength(256)]
        [Display(Name = "Project Home Page URL")]
        [DataType(DataType.Url)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string ProjectUrl { get; set; }

        [StringLength(512)]
        [Display(Name = "Authors")]
        [Subtext("(Authors: use a comma-separated list - example: 'Annie, Bob, Charlie')")]
        [DataType(DataType.Text)]
        public string Authors { get; set; }

        [StringLength(512)]
        [Display(Name = "Copyright")]
        [DataType(DataType.Text)]
        public string Copyright { get; set; }

        [StringLength(1024)]
        [DataType(DataType.Text)]
        [Display(Name = "Tags")]
        [Subtext("(Tags: use space delimited keywords - example: 'CommandLine ASP.NET Proxies Scaffolding')")]
        public string Tags { get; set; }

        [Display(Name = "Release Notes [for this version]")]
        [DataType(DataType.MultilineText)]
        public string ReleaseNotes { get; set; }
    }
}
