using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NuGetGallery
{
    public class EditPackageRegistrationRequest
    {
        public EditPackageRegistrationRequest()
        {
        }

        public EditPackageRegistrationRequest(Package p)
        {
            DefaultTitle = p.PackageRegistration.DefaultTitle;
            Description = p.GetCurrentDescription();
            Summary = p.GetCurrentSummary();
            Tags = p.GetCurrentTags().Flatten();
            IconUrl = p.GetCurrentIconUrl();
            ProjectUrl = p.GetCurrentProjectUrl();
            SourceCodeUrl = p.PackageRegistration.SourceCodeUrl;
            IssueTrackerUrl = p.PackageRegistration.IssueTrackerUrl;
        }

        [StringLength(256)]
        [Display(Name = "Title")]
        [Subtext("Acts as default title for versions that don't set a title.")]
        public string DefaultTitle { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Summary")]
        [Subtext("Summary is a short text shown in package search results.")]
        public string Summary { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [StringLength(256)]
        [Display(Name = "Icon URL")]
        [DataType(DataType.ImageUrl)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string IconUrl { get; set; }

        [StringLength(256)]
        [Display(Name = "Project Homepage URL")]
        [DataType(DataType.Url)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string ProjectUrl { get; set; }

        [StringLength(256)]
        [Display(Name = "Source Code Repository URL (e.g. GitHub)")]
        [DataType(DataType.Url)]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string SourceCodeUrl { get; set; }

        [StringLength(256)]
        [DataType(DataType.Url)]
        [Display(Name = "Issue Tracker URL")]
        [RegularExpression(Constants.UrlValidationRegEx, ErrorMessage = "This doesn't appear to be a valid URL")]
        public string IssueTrackerUrl { get; set; }

        [StringLength(1024)]
        [DataType(DataType.Text)]
        public string Tags { get; set; }

        internal void UpdatePackageRegistration(PackageRegistration packageRegistration, IEntitiesContext context)
        {
            packageRegistration.DefaultTitle = DefaultTitle;
            packageRegistration.Description = Description;
            packageRegistration.Summary = Summary;
            packageRegistration.IconUrl = IconUrl;
            packageRegistration.ProjectUrl = ProjectUrl;
            packageRegistration.SourceCodeUrl = SourceCodeUrl;
            packageRegistration.IssueTrackerUrl = IssueTrackerUrl;

            packageRegistration.Tags.Clear();
            packageRegistration.FlattenedTags = null;
            if (Tags != null)
            {
                var tagNames = Tags.Split(new[] { ',', ' ' })
                    .Select(name => name.Trim()).Where(name => !String.IsNullOrWhiteSpace(name))
                    .ToArray();

                var knownTags = context.Set<Tag>().Where(t => tagNames.Contains(t.Name)).ToList();
                HashSet<string> foundNames = new HashSet<string>(knownTags.Select(tag => tag.Name), StringComparer.OrdinalIgnoreCase);

                foreach (var tag in knownTags)
                {
                    packageRegistration.Tags.Add(tag);
                }

                foreach (var newName in tagNames.Where(name => !foundNames.Contains(name)))
                {
                    packageRegistration.Tags.Add(new Tag { Name = newName });
                }

                packageRegistration.FlattenedTags = packageRegistration.Tags.Flatten();
            }

            packageRegistration.LastUpdated = DateTime.UtcNow;  // Flagged for re-indexing
        }
    }
}