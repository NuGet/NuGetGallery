using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Publish;

namespace PowerShellPublishTests
{
    internal class MockIRegistrationOwnership : IRegistrationOwnership
    {
        public bool IsAuthorized
        {
            get { throw new NotImplementedException(); }
        }

        public Task<bool> IsUserAdministrator()
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsTenantEnabled()
        {
            throw new NotImplementedException();
        }

        public Task AddTenant()
        {
            throw new NotImplementedException();
        }

        public Task RemoveTenant()
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsAuthorizedToRegistration(string domain, string id)
        {
            throw new NotImplementedException();
        }

        public Task AddRegistrationOwner(string domain, string id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RegistrationExists(string domain, string id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PackageExists(string domain, string id, string version)
        {
            throw new NotImplementedException();
        }

        public string GetUserId()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetUserName()
        {
            throw new NotImplementedException();
        }

        public string GetTenantId()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetTenantName()
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetDomains()
        {
            throw new NotImplementedException();
        }
    }
}
