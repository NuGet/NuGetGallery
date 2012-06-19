using System;
using System.Data.Entity;
using System.Linq;
using OData.Linq;

namespace NuGetGallery
{
    public static class PackageExtensions
    {
        private static readonly DateTime magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions = new DateTime(1900, 1, 1, 0, 0, 0);

        public static IQueryable<V1FeedPackage> ToV1FeedPackageQuery(this IQueryable<Package> packages, string siteRoot)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                     .WithoutNullPropagation()
                     .Include(p => p.PackageRegistration)
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
                         ExternalPackageUrl = p.ExternalPackageUrl,
                         GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                         IconUrl = p.IconUrl,
                         IsLatestVersion = p.IsLatestStable,
                         LastUpdated = p.LastUpdated,
                         LicenseUrl = p.LicenseUrl,
                         PackageHash = p.Hash,
                         PackageHashAlgorithm = p.HashAlgorithm,
                         PackageSize = p.PackageFileSize,
                         ProjectUrl = p.ProjectUrl,
                         Published = p.Listed ? p.Published : magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions,
                         ReleaseNotes = p.ReleaseNotes,
                         ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                         RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                         Summary = p.Summary,
                         Tags = p.Tags == null ? null : " " + p.Tags.Trim() + " ", // In the current feed, tags are padded with a single leading and trailing space 
                         Title = p.Title ?? p.PackageRegistration.Id, // Need to do this since the older feed always showed a title.
                         VersionDownloadCount = p.DownloadCount,
                         Rating = 0
                     });
        }

        public static IQueryable<V2FeedPackage> ToV2FeedPackageQuery(this IQueryable<Package> packages, string siteRoot)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                     .WithoutNullPropagation()
                     .Include(p => p.PackageRegistration)
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
                         GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                         IconUrl = p.IconUrl,
                         IsLatestVersion = p.IsLatestStable, // To maintain parity with v1 behavior of the feed, IsLatestVersion would only be used for stable versions.
                         IsAbsoluteLatestVersion = p.IsLatest,
                         IsPrerelease = p.IsPrerelease,
                         LastUpdated = p.LastUpdated,
                         LicenseUrl = p.LicenseUrl,
                         Language = null,
                         PackageHash = p.Hash,
                         PackageHashAlgorithm = p.HashAlgorithm,
                         PackageSize = p.PackageFileSize,
                         ProjectUrl = p.ProjectUrl,
                         ReleaseNotes = p.ReleaseNotes,
                         ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                         RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                         Published = p.Listed ? p.Published : magicDateThatActuallyMeansUnpublishedBecauseOfLegacyDecisions,
                         Summary = p.Summary,
                         Tags = p.Tags,
                         Title = p.Title,
                         VersionDownloadCount = p.DownloadCount
                     });
        }

        private static string EnsureTrailingSlash(string siteRoot)
        {
            if (!siteRoot.EndsWith("/", StringComparison.Ordinal))
            {
                siteRoot = siteRoot + '/';
            }
            return siteRoot;
        }
    }
}