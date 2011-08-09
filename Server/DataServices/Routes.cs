using System.Data.Services;
using System.ServiceModel.Activation;
using System.Web.Routing;
using Ninject;
using NuGet.Server.DataServices;
using NuGet.Server.Infrastructure;
using RouteMagic;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGet.Server.NuGetRoutes), "Start")]

namespace NuGet.Server {
    public static class NuGetRoutes {
        public static void Start() {
            MapRoutes(RouteTable.Routes);
        }

        private static void MapRoutes(RouteCollection routes) {
            // Route to create a new package
            routes.MapDelegate("CreatePackage",
                               "PackageFiles/{apiKey}/nupkg",
                               context => CreatePackageService().CreatePackage(context.HttpContext));

            // Route to publish a package
            routes.MapDelegate("PublishPackage",
                               "PublishedPackages/Publish",
                               context => CreatePackageService().PublishPackage(context.HttpContext));

            // Route to delete packages
            routes.MapDelegate("DeletePackage",
                               "Packages/{apiKey}/{packageId}/{version}",
                               context => CreatePackageService().DeletePackage(context.HttpContext));

#if DEBUG
            // The default route is http://{root}/nuget/Packages
            var factory = new DataServiceHostFactory();
            var serviceRoute = new ServiceRoute("nuget", factory, typeof(Packages));
            serviceRoute.Defaults = new RouteValueDictionary { { "serviceType", "odata" } };
            serviceRoute.Constraints = new RouteValueDictionary { { "serviceType", "odata" } };
            routes.Add("nuget", serviceRoute);
#endif
        }

        private static PackageService CreatePackageService() {
            return NinjectBootstrapper.Kernel.Get<PackageService>();
        }
    }
}
