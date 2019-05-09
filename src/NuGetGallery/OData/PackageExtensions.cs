// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.OData.QueryInterceptors;
using QueryInterceptor;

namespace NuGetGallery.OData
{
    public static class PackageExtensions
    {
        internal static readonly DateTime UnpublishedDate = new DateTime(1900, 1, 1, 0, 0, 0);

        public static IQueryable<V1FeedPackage> ToV1FeedPackageQuery(this IQueryable<Package> packages, string siteRoot, IIconUrlProvider iconUrlProvider)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages
                .Include(p => p.PackageRegistration)
                .Select(
                    p => new V1FeedPackage
                        {
                            Id = p.PackageRegistration.Id,
                            Version = p.Version,
                            Authors = p.FlattenedAuthors,
                            Copyright = p.Copyright,
                            Created = p.Created,
                            Dependencies = p.FlattenedDependencies,
                            Description = p.Description,
                            DownloadCount = p.PackageRegistration.DownloadCount,
                            ExternalPackageUrl = null,
                            GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.Version,
                            IconUrl = iconUrlProvider.GetIconUrlString(p),
                            // We do not project SemVer2 equivalent of IsLatestStable on v1 feeds
                            // as SemVer2 is not supported on this endpoint.
                            IsLatestVersion = p.IsLatestStable,
                            Language = p.Language,
                            LastUpdated = p.LastUpdated,
                            LicenseUrl = p.LicenseUrl,
                            PackageHash = p.Hash,
                            PackageHashAlgorithm = p.HashAlgorithm,
                            PackageSize = p.PackageFileSize,
                            ProjectUrl = p.ProjectUrl,
                            Published = p.Listed ? p.Published : UnpublishedDate,
                            ReleaseNotes = p.ReleaseNotes,
                            ReportAbuseUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.NormalizedVersion + "/ReportAbuse",
                            RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                            Summary = p.Summary,
                            Tags = p.Tags == null ? null : " " + p.Tags.Trim() + " ",
                            // In the current feed, tags are padded with a single leading and trailing space
                            Title = p.Title ?? p.PackageRegistration.Id, // Need to do this since the older feed always showed a title.
                            VersionDownloadCount = p.DownloadCount,
                            Rating = 0
                        });
        }

        public static IQueryable<V2FeedPackage> ToV2FeedPackageQuery(
            this IQueryable<Package> packages,
            string siteRoot,
            bool includeLicenseReport,
            int? semVerLevelKey,
            IIconUrlProvider iconUrlProvider)
        {
            return ProjectV2FeedPackage(
                packages.Include(p => p.PackageRegistration),
                siteRoot,
                includeLicenseReport,
                semVerLevelKey,
                iconUrlProvider);
        }

        // Does the actual projection of a Package object to a V2FeedPackage.
        // This is in a separate method for testability
        internal static IQueryable<V2FeedPackage> ProjectV2FeedPackage(
            this IQueryable<Package> packages,
            string siteRoot,
            bool includeLicenseReport,
            int? semVerLevelKey,
            IIconUrlProvider iconUrlProvider)
        {
            siteRoot = EnsureTrailingSlash(siteRoot);
            return packages.Select(p => new V2FeedPackage
                {
                    Id = p.PackageRegistration.Id,
                    Version = p.Version,
                    NormalizedVersion = p.NormalizedVersion,
                    Authors = p.FlattenedAuthors,
                    Copyright = p.Copyright,
                    Created = p.Created,
                    Dependencies = p.FlattenedDependencies,
                    Description = p.Description,
                    DownloadCount = p.PackageRegistration.DownloadCount,
                    GalleryDetailsUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.NormalizedVersion,
                    IconUrl = iconUrlProvider.GetIconUrlString(p),
                    // We do not expose the internal IsLatestSemVer2 and IsLatestStableSemVer2 properties; 
                    // instead the existing IsAbsoluteLatestVersion and IsLatestVersion properties will be updated based on the 
                    // semver-level supported by the caller.
                    IsLatestVersion = semVerLevelKey == SemVerLevelKey.SemVer2 ? p.IsLatestStableSemVer2 : p.IsLatestStable,
                    // To maintain parity with v1 behavior of the feed, IsLatestVersion would only be used for stable versions.
                    IsAbsoluteLatestVersion = semVerLevelKey == SemVerLevelKey.SemVer2 ? p.IsLatestSemVer2 : p.IsLatest,
                    IsPrerelease = p.IsPrerelease,
                    LastUpdated = p.LastUpdated,
                    Language = p.Language,
                    PackageHash = p.Hash,
                    PackageHashAlgorithm = p.HashAlgorithm,
                    PackageSize = p.PackageFileSize,
                    ProjectUrl = p.ProjectUrl,
                    ReleaseNotes = p.ReleaseNotes,
                    ReportAbuseUrl = siteRoot + "packages/" + p.PackageRegistration.Id + "/" + p.NormalizedVersion + "/ReportAbuse",
                    RequireLicenseAcceptance = p.RequiresLicenseAcceptance,
                    Published = p.Listed ? p.Published : UnpublishedDate,
                    Summary = p.Summary,
                    Tags = p.Tags,
                    Title = p.Title,
                    VersionDownloadCount = p.DownloadCount,
                    MinClientVersion = p.MinClientVersion,
                    LastEdited = p.LastEdited,

                    // License Report Information
                    LicenseUrl = p.LicenseUrl,
                    LicenseNames = (!includeLicenseReport || p.HideLicenseReport) ? null : p.LicenseNames,
                    LicenseReportUrl = (!includeLicenseReport || p.HideLicenseReport) ? null : p.LicenseReportUrl
                });
        }

        internal static IQueryable<TVal> WithoutSortOnColumn<TVal>(
            this IQueryable<TVal> feedQuery, 
            string columnName, 
            bool confirmToIgnoreSort = true)
        {
            return confirmToIgnoreSort ? feedQuery.InterceptWith(new ODataRemoveSorter(columnName)) : feedQuery;
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
