using System;
using System.Security.Principal;
using System.Web.Configuration;

namespace NuGet.Server.Infrastructure {
    public class PackageAuthenticationService : IPackageAuthenticationService {
        public bool IsAuthenticated(IPrincipal user, string apiKey, string packageId) {
            string settingsApiKey = WebConfigurationManager.AppSettings["apiKey"];

            // No api key, no-one can push
            if (String.IsNullOrEmpty(settingsApiKey)) {
                return false;
            }

            return apiKey.Equals(settingsApiKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
