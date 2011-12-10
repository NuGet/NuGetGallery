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
        public string ExternalPackageUrl { get; set; }
        public string GalleryDetailsUrl { get; set; }
        public string IconUrl { get; set; }
        public bool IsLatestVersion { get; set; }
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
        public int RatingsCount
        {
            get { return 0; }
        }

        public int VersionRatingsCount
        {
            get { return 0; }
        }

        public double Rating
        {
            get;
            set;
        }

        public double VersionRating
        {
            get { return 0.0; }
        }

        public string Categories { get { return String.Empty; } }
        public string Language { get { return ""; } }
        public string PackageType { get { return "Package"; } }
        public decimal Price { get { return 0; } }
        public bool Prerelease { get { return false; } }
    }
}