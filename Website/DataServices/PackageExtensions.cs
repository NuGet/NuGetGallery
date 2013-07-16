using System;
using System.Data.Entity;
using System.Linq;
using OData.Linq;
using QueryInterceptor;

namespace NuGetGallery
{
    public static class PackageExtensions
    {
        private static readonly DateTime UnpublishedDate = new DateTime(1900, 1, 1, 0, 0, 0);

        public static IQueryable<V1FeedPackage> ToV1FeedPackageQuery(this IQueryable<Package> packages, string siteRoot)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                .Include(p => p.PackageRegistration)
                .Include(p => p.Metadata)
                .WithoutNullPropagation()
                .Select(
                    p => new V1FeedPackage
                        {
                            Id = p.PackageRegistration.Id,
                            Version = p.Version,
                            Authors = p.Metadata.Authors,
                            Copyright = p.Metadata.Copyright,
                            Created = p.Created,
                            Dependencies = p.FlattenedDependencies,
                            Description = p.Metadata.Description,
                            DownloadCount = p.PackageRegistration.DownloadCount,
                            ExternalPackageUrl = null,
                            GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                            IconUrl = p.Metadata.IconUrl,
                            IsLatestVersion = p.IsLatestStable,
                            Language = p.Language,
                            LastUpdated = p.LastUpdated,
                            LicenseUrl = p.Metadata.LicenseUrl,
                            PackageHash = p.Metadata.Hash,
                            PackageHashAlgorithm = p.Metadata.HashAlgorithm,
                            PackageSize = p.Metadata.PackageFileSize,
                            ProjectUrl = p.Metadata.ProjectUrl,
                            Published = p.Listed ? p.Published : UnpublishedDate,
                            ReleaseNotes = p.Metadata.ReleaseNotes,
                            ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                            RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                            Summary = p.Metadata.Summary,
                            Tags = p.Metadata.Tags == null ? null : " " + p.Metadata.Tags.Trim() + " ",
                            // In the current feed, tags are padded with a single leading and trailing space 
                            Title = p.Metadata.Title ?? p.PackageRegistration.Id, // Need to do this since the older feed always showed a title.
                            VersionDownloadCount = p.DownloadCount,
                            Rating = 0
                        });
        }

        public static IQueryable<V2FeedPackage> ToV2FeedPackageQuery(this IQueryable<Package> packages, string siteRoot)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                .Include(p => p.PackageRegistration)
                .Include(p => p.Metadata)
                .WithoutNullPropagation()

                // Duplicate of the code above, because EF tries to translate a call to ToV2FeedPackage into a Database operation and fails.
                .Select(p => new V2FeedPackage
                {
                    Id = p.PackageRegistration.Id,
                    Version = p.Version,
                    Authors = p.Metadata.Authors,
                    Copyright = p.Metadata.Copyright,
                    Created = p.Created,
                    Dependencies = p.FlattenedDependencies,
                    Description = p.Metadata.Description,
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                    IconUrl = p.Metadata.IconUrl,
                    IsLatestVersion = p.IsLatestStable,
                    // To maintain parity with v1 behavior of the feed, IsLatestVersion would only be used for stable versions.
                    IsAbsoluteLatestVersion = p.IsLatest,
                    IsPrerelease = p.IsPrerelease,
                    LastUpdated = p.LastUpdated,
                    LicenseUrl = p.Metadata.LicenseUrl,
                    Language = p.Language,
                    PackageHash = p.Metadata.Hash,
                    PackageHashAlgorithm = p.Metadata.HashAlgorithm,
                    PackageSize = p.Metadata.PackageFileSize,
                    ProjectUrl = p.Metadata.ProjectUrl,
                    ReleaseNotes = p.Metadata.ReleaseNotes,
                    ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                    RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                    Published = p.Listed ? p.Published : UnpublishedDate,
                    Summary = p.Metadata.Summary,
                    Tags = p.Metadata.Tags,
                    Title = p.Metadata.Title,
                    VersionDownloadCount = p.DownloadCount,
                    MinClientVersion = p.MinClientVersion,
                });
        }

        internal static IQueryable<TVal> WithoutVersionSort<TVal>(this IQueryable<TVal> feedQuery)
        {
            return feedQuery.InterceptWith(new ODataRemoveVersionSorter());
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