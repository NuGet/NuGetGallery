using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public interface IRegistrationOwnership
    {
        bool IsAuthenticated { get; }
        Task<bool> IsUserAdministrator();

        Task EnableTenant();
        Task DisableTenant();

        Task<bool> HasTenantEnabled();

        Task AddVersion(string prefix, string id, string version);

        Task<bool> HasOwner(string prefix, string id);
        Task<bool> HasRegistration(string prefix, string id);
        Task<bool> HasVersion(string prefix, string id, string version);

        string GetUserId();
        Task<string> GetUserName();

        string GetTenantId();
        Task<string> GetTenantName();

        Task<IEnumerable<string>> GetDomains();
        Task<IEnumerable<string>> GetTenants();

        Task<string> GetPublisherName();
    }
}