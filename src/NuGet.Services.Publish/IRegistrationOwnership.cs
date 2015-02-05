using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public interface IRegistrationOwnership
    {
        bool IsAuthorized { get; }

        Task<bool> RegistrationExists(string id);
        Task<bool> IsAuthorizedToRegistration(string id);
        Task CreateRegistration(string id);
        Task DeleteRegistration(string id);
        Task AddRegistrationOwner(string id);

        Task<bool> PackageExists(string id, string version);

        string GetUserId();
        Task<string> GetUserName();
        string GetTenantId();
        Task<string> GetTenantName();

        Task<IList<string>> GetDomains();
        Task<IList<string>> GetRegistrations();
    }
}