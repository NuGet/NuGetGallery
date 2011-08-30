using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NuGetGallery {
    public class Package : IEntity {
        public Package() {
            Authors = new HashSet<PackageAuthor>();
            Dependencies = new HashSet<PackageDependency>();
        }

        public int Key { get; set; }

        public PackageRegistration PackageRegistration { get; set; }
        public int PackageRegistrationKey { get; set; }

        public virtual ICollection<PackageStatistics> DownloadStatistics { get; set; }
        public virtual ICollection<PackageAuthor> Authors { get; set; }
        public string Copyright { get; set; }
        public DateTime Created { get; set; }
        public virtual ICollection<PackageDependency> Dependencies { get; set; }
        public int DownloadCount { get; set; }
        public string ExternalPackageUrl { get; set; }
        public string HashAlgorithm { get; set; }
        public string Hash { get; set; }
        public string IconUrl { get; set; }
        public bool IsLatest { get; set; }
        public bool IsPrerelease { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LicenseUrl { get; set; }
        public DateTime? Published { get; set; }
        public long PackageFileSize { get; set; }
        public string ProjectUrl { get; set; }
        public bool RequiresLicenseAcceptance { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }


        private string _description;
        public string Description
        {
            get { return _description; }
            set
            {
                value = ParseNewLines(value);
                _description = value;
            }
        }

        // TODO: it would be nice if we could change the feed so that we don't need to flatten authors and dependencies
        public string FlattenedAuthors { get; set; }
        public string FlattenedDependencies { get; set; }

        private static string ParseNewLines(string value) {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var signature = string.Copy(value ?? "");
            signature = Regex.Replace(signature, @"\r\n", string.Format("<br{0}", " />"));
            signature = Regex.Replace(signature, @"\r", string.Format("<br{0}", " />"));
            signature = Regex.Replace(signature, @"\n", string.Format("<br{0}", " />"));
            return signature;
        }
    }
}