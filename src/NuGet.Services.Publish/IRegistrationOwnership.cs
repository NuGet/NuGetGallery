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

        Task AddVersion(string domain, string id, string version);

        Task<bool> HasOwner(string domain, string id);
        Task<bool> HasRegistration(string domain, string id);
        Task<bool> HasVersion(string domain, string id, string version);

        string GetUserId();
        Task<string> GetUserName();
        string GetTenantId();
        Task<string> GetTenantName();

        Task<IList<string>> GetDomains();

        Task<string> GetPublisherName();
    }
}