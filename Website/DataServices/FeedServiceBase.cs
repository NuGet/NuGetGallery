using System;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.IO;
using System.ServiceModel;
using System.Web.Mvc;

namespace NuGetGallery
{
    // TODO: make this work for both packages and screen shots?

    // TODO: Disable for live service
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public abstract class FeedServiceBase<VxPackage> : DataService<FeedContext<VxPackage>>, IDataServiceStreamProvider, IServiceProvider
    {
        private readonly IEntityRepository<Package> packageRepo;

        public FeedServiceBase()
        {
            // TODO: See if there is a way to do proper DI with data services
            packageRepo = DependencyResolver.Current.GetService<IEntityRepository<Package>>();
        }

        protected IEntityRepository<Package> PackageRepo
        {
            get
            {
                return packageRepo;
            }
        }

        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config)
        {
            config.SetServiceOperationAccessRule("Search", ServiceOperationRights.AllRead);
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);
            config.SetEntitySetPageSize("Packages", 100);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        protected abstract override FeedContext<VxPackage> CreateDataSource();

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
    }
}
