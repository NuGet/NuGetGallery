using System.Security.Principal;

namespace NuGet.Server.Infrastructure {
    public interface IPackageAuthenticationService {
        bool IsAuthenticated(IPrincipal user, string apiKey, string packageId);
    }
}