using System;
using System.Web;
using System.Web.Routing;
using NuGet.Server.Infrastructure;

namespace NuGet.Server {
    public class PackageService {
        private readonly IServerPackageRepository _serverRepository;
        private readonly IPackageAuthenticationService _authenticationService;

        public PackageService(IServerPackageRepository repository,
                              IPackageAuthenticationService authenticationService) {
            _serverRepository = repository;
            _authenticationService = authenticationService;
        }

        public void CreatePackage(HttpContextBase context) {
            RouteData routeData = GetRouteData(context);
            // Get the api key from the route
            string apiKey = routeData.GetRequiredString("apiKey");

            // Get the package from the request body
            var package = new ZipPackage(context.Request.InputStream);

            // Make sure they can access this package
            Authenticate(context, apiKey, package.Id,
                         () => _serverRepository.AddPackage(package));
        }

        public void PublishPackage(HttpContextBase context) {
            // No-op
        }

        public void DeletePackage(HttpContextBase context) {
            // Only accept delete requests
            if (!context.Request.HttpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase)) {
                context.Response.StatusCode = 404;
                return;
            }

            RouteData routeData = GetRouteData(context);

            // Extract the apiKey, packageId and make sure the version if a valid version string
            // (fail to parse if it's not)
            string apiKey = routeData.GetRequiredString("apiKey");
            string packageId = routeData.GetRequiredString("packageId");
            var version = new Version(routeData.GetRequiredString("version"));

            // Make sure they can access this package
            Authenticate(context, apiKey, packageId,
                         () => _serverRepository.RemovePackage(packageId, version));
        }

        private void Authenticate(HttpContextBase context, string apiKey, string packageId, Action action) {
            if (_authenticationService.IsAuthenticated(context.User, apiKey, packageId)) {
                action();
            }
            else {
                WriteAccessDenied(context, packageId);
            }
        }

        private static void WriteAccessDenied(HttpContextBase context, string packageId) {
            context.Response.StatusCode = 401;
            context.Response.Write(String.Format("Access denied for package '{0}'.", packageId));
        }

        private RouteData GetRouteData(HttpContextBase context) {
            return RouteTable.Routes.GetRouteData(context);
        }
    }
}