using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public interface IRegistrationOwnership
    {
        bool IsAuthorized { get; }

        Task<bool> IsAuthorizedToRegistration(string domain, string id);
        Task AddRegistrationOwner(string domain, string id);

        Task<bool> RegistrationExists(string domain, string id);
        Task<bool> PackageExists(string domain, string id, string version);

        string GetUserId();
        Task<string> GetUserName();
        string GetTenantId();
        Task<string> GetTenantName();

        Task<IList<string>> GetDomains();
    }
}