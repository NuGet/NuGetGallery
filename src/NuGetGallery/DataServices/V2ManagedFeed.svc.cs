using System;
using System.Data.Entity;
using System.Data.Services;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery;
using NuGetGallery.Configuration;
using QueryInterceptor;
using System.Collections.Generic;

namespace NuGetGallery
{
    [RewriteBaseUrlMessageInspector]
    public class V2ManagedFeed : FeedServiceBase<V2FeedContext, V2FeedPackage>
    {
        private const int FeedVersion = 2;

        private IManageFeedService _manageFeedService;

        public V2ManagedFeed()
            : this(DependencyResolver.Current.GetService<IManageFeedService>())
        {
        }

        public V2ManagedFeed(IManageFeedService manageFeedService)
            : base()
        {
            _manageFeedService = manageFeedService;
        }

        public V2ManagedFeed(IEntitiesContext entities, IEntityRepository<Package> repo, ConfigurationService configuration, ISearchService searchService, IManageFeedService manageFeedService)
            : base(entities, repo, configuration, searchService)
        {
            _manageFeedService = manageFeedService;
        }

        protected override V2FeedContext CreateDataSource()
        {
            var feedName = GetFeedName();

            var feedPackages = _manageFeedService.GetFeedPackages(feedName);

            return new V2FeedContext
                {
                    Packages = ProjectV2FeedPackage(feedPackages, Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses)
                        .InterceptWith(new NormalizeVersionInterceptor())
                };
        }

        public static void InitializeService(DataServiceConfiguration config)
        {
            InitializeServiceBase(config);
        }

        [WebGet]
        public IQueryable<V2FeedPackage> FindPackagesById(string id)
        {
            var feedName = GetFeedName();
            IQueryable<FeedPackage> packagesQuery = _manageFeedService.GetFeedPackages(feedName)
                .Where(fp => fp.Package.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            IQueryable<V2FeedPackage> projectedV2FeedPackageQuery = ProjectV2FeedPackage(packagesQuery,
                Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses);

            return projectedV2FeedPackageQuery;
        }

        private string GetFeedName()
        {
            var feedName = HttpContext.Request.QueryString["name"];
            return feedName;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "targetFramework", Justification = "We can't change it because it's now part of the contract of this service method.")]
        [WebGet]
        public IQueryable<V2FeedPackage> Search(string searchTerm, string targetFramework, bool includePrerelease)
        {
            var feedName = GetFeedName();

            var feedPackages = _manageFeedService.GetFeedPackages(feedName);

            if (!includePrerelease)
            {
                feedPackages = feedPackages.Where(fp => !fp.Package.IsPrerelease);
            }

            IQueryable<FeedPackage> packagesQuery = feedPackages.Search(searchTerm);

            IQueryable<V2FeedPackage> projectedV2FeedPackageQuery = ProjectV2FeedPackage(packagesQuery, Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses);

            return projectedV2FeedPackageQuery;

            //return SearchAdaptor.SearchCore(
            //        SearchService,
            //        HttpContext.Request,
            //        packages,
            //        searchTerm,
            //        targetFramework,
            //        includePrerelease,
            //        null)
            //    .Result
            //    .ToV2FeedPackageQuery(Configuration.GetSiteRoot(UseHttps()), Configuration.Features.FriendlyLicenses);
        }

        public override Uri GetReadStreamUri(
            object entity,
            DataServiceOperationContext operationContext)
        {
            var package = (V2FeedPackage)entity;
            var urlHelper = new UrlHelper(new RequestContext(HttpContext, new RouteData()));

            string url = urlHelper.PackageDownload(FeedVersion, package.Id, package.Version);

            return new Uri(url, UriKind.Absolute);
        }

        internal static readonly DateTime UnpublishedDate = new DateTime(1900, 1, 1, 0, 0, 0);

        static IQueryable<V2FeedPackage> ProjectV2FeedPackage(IQueryable<FeedPackage> packages, string siteRoot, bool includeLicenseReport)
        {
            siteRoot = siteRoot.TrimEnd('/') + '/';

            var v2FeedPackages = packages
                .Select(fp => new V2FeedPackage
            {
                Id = fp.Package.PackageRegistration.Id,
                Version = fp.Package.Version,
                NormalizedVersion = fp.Package.NormalizedVersion,
                Authors = fp.Package.FlattenedAuthors,
                Copyright = fp.Package.Copyright,
                Created = fp.Package.Created,
                Dependencies = fp.Package.FlattenedDependencies,
                Description = fp.Package.Description,
                DownloadCount = fp.Package.PackageRegistration.DownloadCount,
                GalleryDetailsUrl = siteRoot + "packages/" + fp.Package.PackageRegistration.Id + "/" + fp.Package.NormalizedVersion,
                IconUrl = fp.Package.IconUrl,
                IsLatestVersion = fp.IsLatestStable,
                IsAbsoluteLatestVersion = fp.IsLatest,
                IsPrerelease = fp.Package.IsPrerelease,
                LastUpdated = fp.Package.LastUpdated,
                Language = fp.Package.Language,
                PackageHash = fp.Package.Hash,
                PackageHashAlgorithm = fp.Package.HashAlgorithm,
                PackageSize = fp.Package.PackageFileSize,
                ProjectUrl = fp.Package.ProjectUrl,
                ReleaseNotes = fp.Package.ReleaseNotes,
                ReportAbuseUrl = siteRoot + "package/ReportAbuse/" + fp.Package.PackageRegistration.Id + "/" + fp.Package.NormalizedVersion,
                RequireLicenseAcceptance = fp.Package.RequiresLicenseAcceptance,
                Published = fp.Package.Listed ? fp.Package.Published : UnpublishedDate,
                Summary = fp.Package.Summary,
                Tags = fp.Package.Tags,
                Title = fp.Package.Title,
                VersionDownloadCount = fp.Package.DownloadCount,
                MinClientVersion = fp.Package.MinClientVersion,
                LastEdited = fp.Package.LastEdited,

                // License Report Information
                LicenseUrl = fp.Package.LicenseUrl,
                LicenseNames = (!includeLicenseReport || fp.Package.HideLicenseReport) ? null : fp.Package.LicenseNames,
                LicenseReportUrl = (!includeLicenseReport || fp.Package.HideLicenseReport) ? null : fp.Package.LicenseReportUrl
            });

            return v2FeedPackages;
        }
    }
}