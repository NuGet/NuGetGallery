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
            VersionTitle = package.Title;
            Authors = package.FlattenedAuthors;
            Copyright = package.Copyright;
            ReleaseNotes = package.ReleaseNotes;
            IsListed = package.Listed;
        }

        [StringLength(256)]
        [Display(Name = "Special Title for this Version (leave blank if none)")]
        [DataType(DataType.Text)]
        public string VersionTitle { get; set; }

        [StringLength(512)]
        [Display(Name = "Authors (for this Version)")]
        [DataType(DataType.Text)]
        public string Authors { get; set; }

        [StringLength(512)]
        [Display(Name = "Copyright (for this Version)")]
        [DataType(DataType.Text)]
        public string Copyright { get; set; }

        [Display(Name = "ReleaseNotes (for this Version)")]
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

            package.Title = VersionTitle;
            package.Copyright = Copyright;
            package.ReleaseNotes = ReleaseNotes;
            foreach (var author in package.Authors.ToArray())
            {
                context.DeleteOnCommit(author);
            }
            package.Authors.Clear();
            package.FlattenedAuthors = null;
            if (Authors != null)
            {
                foreach (var author in Authors.Split(',')
                    .Select(name => name.Trim()).Where(name => !String.IsNullOrWhiteSpace(name))
                    .Select(name => new PackageAuthor { Name = name }))
                {
                    package.Authors.Add(author);
                }

                package.FlattenedAuthors = package.Authors.Flatten();
            }

            package.LastUpdated = DateTime.UtcNow; // Flagged for re-indexing
        }
    }
}