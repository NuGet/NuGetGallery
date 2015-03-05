using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class NoSecurityRegistrationOwnership : IRegistrationOwnership
    {
        public bool IsAuthenticated
        {
            get { return true; }
        }

        public Task<bool> IsUserAdministrator()
        {
            return Task.FromResult<bool>(true);
        }

        public Task EnableTenant()
        {
            return Task.FromResult(0);
        }

        public Task DisableTenant()
        {
            return Task.FromResult(0);
        }

        public Task<bool> HasTenantEnabled()
        {
            return Task.FromResult<bool>(true);
        }

        public Task AddVersion(string prefix, string id, string version)
        {
            return Task.FromResult(0);
        }

        public Task<bool> HasOwner(string prefix, string id)
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> HasRegistration(string prefix, string id)
        {
            return Task.FromResult<bool>(false);
        }

        public Task<bool> HasVersion(string prefix, string id, string version)
        {
            return Task.FromResult<bool>(false);
        }

        public string GetUserId()
        {
            return "unknown";
        }

        public Task<string> GetUserName()
        {
            return Task.FromResult("unknown");
        }

        public string GetTenantId()
        {
            return "unknown";
        }

        public Task<string> GetTenantName()
        {
            return Task.FromResult("unknown");
        }

        public Task<IEnumerable<string>> GetDomains()
        {
            return Task.FromResult<IEnumerable<string>>(new List<string> { "domain1", "domain2", "domain3" });
        }

        public Task<IEnumerable<string>> GetTenants()
        {
            return Task.FromResult<IEnumerable<string>>(new List<string> { "tenant1", "tenant2", "tenant3" });
        }

        public Task<string> GetPublisherName()
        {
            return Task.FromResult("unknown");
        }
    }
}