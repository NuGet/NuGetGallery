﻿using System;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.IO;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Data;
using NuGetGallery.Data.Model;

namespace NuGetGallery
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public abstract class FeedServiceBase<TPackage> : DataService<FeedContext<TPackage>>, IDataServiceStreamProvider, IServiceProvider
    {
        private readonly IConfiguration _configuration;

        private readonly IEntitiesContext _entities;
        private readonly IEntityRepository<Package> _packageRepository;
        private readonly ISearchService _searchService;
        private HttpContextBase _httpContext;

        protected FeedServiceBase()
            : this(DependencyResolver.Current.GetService<IEntitiesContext>(),
                   DependencyResolver.Current.GetService<IEntityRepository<Package>>(),
                   DependencyResolver.Current.GetService<IConfiguration>(),
                   DependencyResolver.Current.GetService<ISearchService>())
        {
        }

        protected FeedServiceBase(
            IEntitiesContext entities,
            IEntityRepository<Package> packageRepository,
            IConfiguration configuration,
            ISearchService searchService)
        {
            _entities = entities;
            _packageRepository = packageRepository;
            _configuration = configuration;
            _searchService = searchService;
        }

        protected IEntitiesContext Entities
        {
            get { return _entities; }
        }

        protected IEntityRepository<Package> PackageRepository
        {
            get { return _packageRepository; }
        }

        protected IConfiguration Configuration
        {
            get { return _configuration; }
        }

        protected ISearchService SearchService
        {
            get { return _searchService; }
        }

        protected internal virtual HttpContextBase HttpContext
        {
            get { return _httpContext ?? new HttpContextWrapper(System.Web.HttpContext.Current); }
            set { _httpContext = value; }
        }

        protected internal string SiteRoot
        {
            get
            {
                string siteRoot = Configuration.GetSiteRoot(UseHttps());
                return EnsureTrailingSlash(siteRoot);
            }
        }

        // This method is called only once to initialize service-wide policies.

        public void DeleteStream(
            object entity,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public Stream GetReadStream(
            object entity,
            string etag,
            bool? checkETagForEquality,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public abstract Uri GetReadStreamUri(
            object entity,
            DataServiceOperationContext operationContext);


        public string GetStreamContentType(
            object entity,
            DataServiceOperationContext operationContext)
        {
            return "application/zip";
        }

        public string GetStreamETag(
            object entity,
            DataServiceOperationContext operationContext)
        {
            return null;
        }

        public Stream GetWriteStream(
            object entity,
            string etag,
            bool? checkETagForEquality,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public string ResolveType(
            string entitySetName,
            DataServiceOperationContext operationContext)
        {
            throw new NotSupportedException();
        }

        public int StreamBufferSize
        {
            get { return 64000; }
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IDataServiceStreamProvider))
            {
                return this;
            }

            return null;
        }

        protected static void InitializeServiceBase(DataServiceConfiguration config)
        {
            config.SetServiceOperationAccessRule("Search", ServiceOperationRights.AllRead);
            config.SetServiceOperationAccessRule("FindPackagesById", ServiceOperationRights.AllRead);
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);
            config.SetEntitySetPageSize("Packages", SearchAdaptor.MaxPageSize);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        protected virtual bool UseHttps()
        {
            return HttpContext.Request.IsSecureConnection;
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