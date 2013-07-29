using System;
using System.Data.Services.Common;

namespace NuGetGallery
{
    [HasStream]
    [DataServiceKey("Id", "Version")]
    [EntityPropertyMapping("Title", SyndicationItemProperty.Title, SyndicationTextContentKind.Plaintext, keepInContent: true)]
    [EntityPropertyMapping("Authors", SyndicationItemProperty.AuthorName, SyndicationTextContentKind.Plaintext, keepInContent: true)]
    [EntityPropertyMapping("LastUpdated", SyndicationItemProperty.Updated, SyndicationTextContentKind.Plaintext, keepInContent: true)]
    [EntityPropertyMapping("Summary", SyndicationItemProperty.Summary, SyndicationTextContentKind.Plaintext, keepInContent: true)]
    public class V1FeedPackage
    {
        public string Id { get; set; }
        public string Version { get; set; }

        public string Authors { get; set; }
        public string Copyright { get; set; }
        public DateTime Created { get; set; }
        public string Dependencies { get; set; }
        public string Description { get; set; }
        public int DownloadCount { get; set; }
        public string ExternalPackageUrl { get; set; } // deprecated: always null/empty
        public string GalleryDetailsUrl { get; set; }
        public string IconUrl { get; set; }
        public bool IsLatestVersion { get; set; }
        public string Language { get; set; }
        public DateTime LastUpdated { get; set; }
        public string LicenseUrl { get; set; }
        public string PackageHash { get; set; }
        public string PackageHashAlgorithm { get; set; }
        public long PackageSize { get; set; }
        public string ProjectUrl { get; set; }
        public DateTime? Published { get; set; }
        public string ReportAbuseUrl { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string ReleaseNotes { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Title { get; set; }
        public int VersionDownloadCount { get; set; }

        // Deprecated properties        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public int RatingsCount
        {
            get { return 0; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public int VersionRatingsCount
        {
            get { return 0; }
        }

        public double Rating { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public double VersionRating
        {
            get { return 0.0; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public string Categories
        {
            get { return String.Empty; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public string PackageType
        {
            get { return "Package"; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public decimal Price
        {
            get { return 0; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This must be an instance property for serialization.")]
        public bool Prerelease
        {
            get { return false; }
        }
    }
}