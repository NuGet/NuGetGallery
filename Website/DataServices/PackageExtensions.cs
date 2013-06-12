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
                .WithoutNullPropagation()
                .Select(
                    p => new V1FeedPackage
                        {
                            Id = p.PackageRegistration.Id,
                            Version = p.Version,
                            Authors = p.GetDescription().Authors,
                            Copyright = p.GetDescription().Copyright,
                            Created = p.Created,
                            Dependencies = p.FlattenedDependencies,
                            Description = p.GetDescription().Description,
                            DownloadCount = p.PackageRegistration.DownloadCount,
                            ExternalPackageUrl = null,
                            GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                            IconUrl = p.GetDescription().IconUrl,
                            IsLatestVersion = p.IsLatestStable,
                            Language = p.Language,
                            LastUpdated = p.LastUpdated,
                            LicenseUrl = p.LicenseUrl,
                            PackageHash = p.GetDescription().Hash,
                            PackageHashAlgorithm = p.GetDescription().HashAlgorithm,
                            PackageSize = p.GetDescription().PackageFileSize,
                            ProjectUrl = p.GetDescription().ProjectUrl,
                            Published = p.Listed ? p.Published : UnpublishedDate,
                            ReleaseNotes = p.GetDescription().ReleaseNotes,
                            ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                            RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                            Summary = p.GetDescription().Summary,
                            Tags = p.GetDescription().Tags == null ? null : " " + p.GetDescription().Tags.Trim() + " ",
                            // In the current feed, tags are padded with a single leading and trailing space 
                            Title = p.GetDescription().Title ?? p.PackageRegistration.Id, // Need to do this since the older feed always showed a title.
                            VersionDownloadCount = p.DownloadCount,
                            Rating = 0
                        });
        }

        public static IQueryable<V2FeedPackage> ToV2FeedPackageQuery(this IQueryable<Package> packages, string siteRoot)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                .Include(p => p.PackageRegistration)
                .WithoutNullPropagation()

                // Duplicate of the code above, because EF tries to translate a call to ToV2FeedPackage into a Database operation and fails.
                .Select(p => new V2FeedPackage
                {
                    Id = p.PackageRegistration.Id,
                    Version = p.Version,
                    Authors = p.GetDescription().Authors,
                    Copyright = p.GetDescription().Copyright,
                    Created = p.Created,
                    Dependencies = p.FlattenedDependencies,
                    Description = p.GetDescription().Description,
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                    IconUrl = p.GetDescription().IconUrl,
                    IsLatestVersion = p.IsLatestStable,
                    // To maintain parity with v1 behavior of the feed, IsLatestVersion would only be used for stable versions.
                    IsAbsoluteLatestVersion = p.IsLatest,
                    IsPrerelease = p.IsPrerelease,
                    LastUpdated = p.LastUpdated,
                    LicenseUrl = p.LicenseUrl,
                    Language = p.Language,
                    PackageHash = p.GetDescription().Hash,
                    PackageHashAlgorithm = p.GetDescription().HashAlgorithm,
                    PackageSize = p.GetDescription().PackageFileSize,
                    ProjectUrl = p.GetDescription().ProjectUrl,
                    ReleaseNotes = p.GetDescription().ReleaseNotes,
                    ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + p.PackageRegistration.Id + "/" + p.Version,
                    RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                    Published = p.Listed ? p.Published : UnpublishedDate,
                    Summary = p.GetDescription().Summary,
                    Tags = p.GetDescription().Tags,
                    Title = p.GetDescription().Title,
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