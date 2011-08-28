using System;
using System.Data.Entity;
using System.Linq;
using OData.Linq;

namespace NuGetGallery {
    public static class PackageExtensions {
        static readonly DateTime magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions = new DateTime(1900, 1, 1, 0, 0, 0);
        public static IQueryable<FeedPackage> ToFeedPackageQuery(this IQueryable<Package> packages) {
            return packages
                     .WithoutNullPropagation()
                     .Include(p => p.PackageRegistration)
                     .Include(p => p.Authors)
                     .Include(p => p.Dependencies)
                     .Include(p => p.DownloadStatistics)
                     .Select(p => new FeedPackage {
                         Id = p.PackageRegistration.Id,
                         Version = p.Version,
                         Authors = p.FlattenedAuthors,
                         Copyright = p.Copyright,
                         Created = p.Created,
                         Dependencies = p.FlattenedDependencies,
                         Description = p.Description,
                         DownloadCount = p.PackageRegistration.DownloadCount,
                         ExternalPackageUri = p.ExternalPackageUrl,
                         // TODO: build the gallery details URL for real
                         GalleryDetailsUrl = "http://localhost",
                         IconUrl = p.IconUrl,
                         IsLatestVersion = p.IsLatest,
                         LastUpdated = p.LastUpdated,
                         LicenseUrl = p.LicenseUrl,
                         PackageHash = p.Hash,
                         PackageHashAlgorithm = p.HashAlgorithm,
                         PackageSize = p.PackageFileSize,
                         ProjectUrl = p.ProjectUrl,
                         Published = p.Published ?? magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions,
                         // TODO: build the report abuse URL for real
                         ReportAbuseUrl = "http://localhost",
                         RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                         Summary = p.Summary,
                         Tags = p.Tags,
                         Title = p.Title,
                         VersionDownloadCount = p.DownloadStatistics.Count,
                     });
        }
    }
}