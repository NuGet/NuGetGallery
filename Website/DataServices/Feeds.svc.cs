using System;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.IO;
using System.Web.Mvc;

namespace NuGetGallery
{
    // TODO: make this work for both packages and screen shots?
    
    // TODO: Disable for live service
    [System.ServiceModel.ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class Feeds : DataService<FeedsContext>, IDataServiceStreamProvider, IServiceProvider 
    {
        readonly IEntityRepository<Package> packageRepo;
        readonly IPackageFileService packageFileSvc;
        
        public Feeds()
        {
            // TODO: See if there is a way to do proper DI with data services
            packageRepo = DependencyResolver.Current.GetService<IEntityRepository<Package>>();
            packageFileSvc = DependencyResolver.Current.GetService<IPackageFileService>();
        }
        
        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config) 
        {
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);

            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        protected override FeedsContext CreateDataSource() 
        {
            return new FeedsContext(packageRepo);
        }

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

        public Uri GetReadStreamUri(
            object entity, 
            DataServiceOperationContext operationContext) 
        {
            var package = (FeedPackage)entity;

            return packageFileSvc.GetDownloadUri(package.Id, package.Version);
        }

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
