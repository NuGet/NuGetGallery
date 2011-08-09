using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.Services.Providers;
using System.IO;
using System.Linq;
using System.ServiceModel.Web;
using Ninject;
using NuGet.Server.Infrastructure;

namespace NuGet.Server.DataServices {
    // Disabled for live service
    [System.ServiceModel.ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class Packages : DataService<PackageContext>, IDataServiceStreamProvider, IServiceProvider {
        private IServerPackageRepository Repository {
            get {
                // It's bad to use the container directly but we aren't in the loop when this 
                // class is created
                return NinjectBootstrapper.Kernel.Get<IServerPackageRepository>();
            }
        }

        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config) {
            config.SetEntitySetAccessRule("Packages", EntitySetRights.AllRead);
            config.SetServiceOperationAccessRule("Search", ServiceOperationRights.AllRead);

            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        protected override PackageContext CreateDataSource() {
            return new PackageContext(Repository);
        }

        public void DeleteStream(object entity, DataServiceOperationContext operationContext) {
            throw new NotSupportedException();
        }

        public Stream GetReadStream(object entity, string etag, bool? checkETagForEquality, DataServiceOperationContext operationContext) {
            throw new NotSupportedException();
        }

        public Uri GetReadStreamUri(object entity, DataServiceOperationContext operationContext) {
            var package = (Package)entity;

            return PackageUtility.GetPackageUrl(package.Path, operationContext.AbsoluteServiceUri);
        }

        public string GetStreamContentType(object entity, DataServiceOperationContext operationContext) {
            return "application/zip";
        }

        public string GetStreamETag(object entity, DataServiceOperationContext operationContext) {
            return null;
        }

        public Stream GetWriteStream(object entity, string etag, bool? checkETagForEquality, DataServiceOperationContext operationContext) {
            throw new NotSupportedException();
        }

        public string ResolveType(string entitySetName, DataServiceOperationContext operationContext) {
            throw new NotSupportedException();
        }

        public int StreamBufferSize {
            get {
                return 64000;
            }
        }

        public object GetService(Type serviceType) {
            if (serviceType == typeof(IDataServiceStreamProvider)) {
                return this;
            }
            return null;
        }

        [WebGet]
        public IQueryable<Package> Search(string searchTerm, string targetFramework) {
            IEnumerable<string> targetFrameworks = String.IsNullOrEmpty(targetFramework) ? Enumerable.Empty<string>() : targetFramework.Split('|');

            return from package in Repository.Search(searchTerm, targetFrameworks)
                   select Repository.GetMetadataPackage(package);
        }
    }
}
