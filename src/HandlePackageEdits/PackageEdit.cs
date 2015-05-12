using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace HandlePackageEdits
{
    public class PackageEdit
    {
        public int Key { get; set; }
        public int PackageKey { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }

        /// <summary>
        /// User who edited the package and thereby caused this edit to be created.
        /// </summary>
        public int UserKey { get; set; }

        /// <summary>
        /// Time this edit was generated
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Count so that the worker role can tell itself not to retry processing this edit forever if it gets stuck.
        /// </summary>
        public int TriedCount { get; set; }
        public string LastError { get; set; }

        public string Title { get; set; }
        public string Authors { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }

        public virtual void ApplyTo(ManifestMetadata metadata)
        {
            metadata.Title = Title;
            metadata.Authors = Authors;
            metadata.Copyright = Copyright;
            metadata.Description = Description;
            metadata.IconUrl = IconUrl;
            if (!String.IsNullOrEmpty(LicenseUrl.Trim()))
            {
                metadata.LicenseUrl = LicenseUrl;
            }
            metadata.ProjectUrl = ProjectUrl;
            metadata.ReleaseNotes = ReleaseNotes;
            metadata.RequireLicenseAcceptance = RequiresLicenseAcceptance;
            metadata.Summary = Summary;
            metadata.Tags = Tags;
        }
    }
}
