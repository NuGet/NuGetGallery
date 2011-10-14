using System;
using System.Data.Entity;
using System.Linq;
using OData.Linq;

namespace NuGetGallery
{
    // TODO: build the report abuse URL for real
    // TODO: build the gallery details URL for real
    public static class PackageExtensions
    {
        private static readonly DateTime magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions = new DateTime(1900, 1, 1, 0, 0, 0);

        public static IQueryable<V1FeedPackage> ToV1FeedPackageQuery(this IQueryable<Package> packages)
        {
            return packages
                     .WithoutNullPropagation()
                     .Include(p => p.PackageRegistration)
                     .Include(p => p.Authors)
                     .Include(p => p.Dependencies)
                     .Include(p => p.DownloadStatistics)
                     .Select(p => new V1FeedPackage
                     {
                         Id = p.PackageRegistration.Id,
                         Version = p.Version,
                         Authors = p.FlattenedAuthors,
                         Copyright = p.Copyright,
                         Created = p.Created,
                         Dependencies = p.FlattenedDependencies,
                         Description = p.Description,
                         DownloadCount = p.PackageRegistration.DownloadCount,
                         ExternalPackageUri = p.ExternalPackageUrl,
                         GalleryDetailsUrl = "http://localhost",
                         IconUrl = p.IconUrl,
                         IsLatestVersion = p.IsLatestStable,
                         LastUpdated = p.LastUpdated,
                         LicenseUrl = p.LicenseUrl,
                         PackageHash = p.Hash,
                         PackageHashAlgorithm = p.HashAlgorithm,
                         PackageSize = p.PackageFileSize,
                         ProjectUrl = p.ProjectUrl,
                         Published = p.Listed ? p.Published : magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions,
                         ReportAbuseUrl = "http://localhost",
                         RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                         Summary = p.Summary,
                         Tags = p.Tags,
                         Title = p.Title,
                         VersionDownloadCount = p.DownloadStatistics.Count,
                     });
        }

        public static IQueryable<V2FeedPackage> ToV2FeedPackageQuery(this IQueryable<Package> packages)
        {
            return packages
                     .WithoutNullPropagation()
                     .Include(p => p.PackageRegistration)
                     .Include(p => p.Authors)
                     .Include(p => p.Dependencies)
                     .Include(p => p.DownloadStatistics)
                     .Select(p => new V2FeedPackage
                     {
                         Id = p.PackageRegistration.Id,
                         Version = p.Version,
                         Authors = p.FlattenedAuthors,
                         Copyright = p.Copyright,
                         Created = p.Created,
                         Dependencies = p.FlattenedDependencies,
                         Description = p.Description,
                         DownloadCount = p.PackageRegistration.DownloadCount,
                         GalleryDetailsUrl = "http://localhost",
                         IconUrl = p.IconUrl,
                         IsLatestVersion = p.IsLatestStable, // To maintain parity with v1 behavior of the feed, IsLatestVersion would only be used for stable versions.
                         IsAbsoluteLatestVersion = p.IsLatest,
                         LastUpdated = p.LastUpdated,
                         LicenseUrl = p.LicenseUrl,
                         PackageHash = p.Hash,
                         PackageHashAlgorithm = p.HashAlgorithm,
                         PackageSize = p.PackageFileSize,
                         ProjectUrl = p.ProjectUrl,
                         ReportAbuseUrl = "http://localhost",
                         RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                         Published = p.Listed ? p.Published : magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions,
                         Summary = p.Summary,
                         Tags = p.Tags,
                         Title = p.Title,
                         VersionDownloadCount = p.DownloadStatistics.Count,
                     });
        }
    }
}