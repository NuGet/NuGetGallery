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
            Title = package.Title;
            IconUrl = package.IconUrl;
            Summary = package.Summary;
            Description = package.Description;
            ProjectUrl = package.ProjectUrl;
            Authors = package.FlattenedAuthors;
            Copyright = package.Copyright;
            ReleaseNotes = package.ReleaseNotes;
            IsListed = package.Listed;
        }

        [StringLength(256)]
        [Display(Name = "Title")]
        [DataType(DataType.Text)]
        public string Title { get; set; }

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

        [Display(Name = "List this version in search results")]
        public bool IsListed { get; set; }

        internal void UpdatePackageVersion(
            Package package,
            IEntitiesContext context,
            IPackageService packageService)
        {
            if (IsListed != package.Listed)
            {
                if (IsListed)
                {
                    packageService.MarkPackageListed(package, commitChanges: false);
                }
                else
                {
                    packageService.MarkPackageUnlisted(package, commitChanges: false);
                }
            }

            package.Title = Title;
            package.IconUrl = IconUrl;
            package.Summary = Summary;
            package.Description = Description;
            package.ProjectUrl = ProjectUrl;

            // authors
            foreach (var author in package.Authors.ToArray())
            {
                context.DeleteOnCommit(author);
            }
            package.Authors.Clear();
            package.FlattenedAuthors = null;
            if (Authors != null)
            {
                foreach (var authorName in Authors.Split(',')
                    .Select(name => name.Trim())
                    .Where(name => !String.IsNullOrWhiteSpace(name)))
                {
                    package.Authors.Add(new PackageAuthor { Name = authorName });
                }

                package.FlattenedAuthors = package.Authors.Flatten();
            }

            package.Copyright = Copyright;
            package.Tags = Tags;
            package.ReleaseNotes = ReleaseNotes;

            package.LastUpdated = DateTime.UtcNow; // Flagged for re-indexing
        }
    }
}