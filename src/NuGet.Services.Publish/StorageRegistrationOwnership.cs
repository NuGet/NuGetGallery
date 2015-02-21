using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Ownership;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class StorageRegistrationOwnership : IRegistrationOwnership
    {
        IOwinContext _context;
        ActiveDirectoryClient _activeDirectoryClient;
        IRegistration _registration;

        public StorageRegistrationOwnership(IOwinContext context, CloudStorageAccount account, string ownershipContainer)
        {
            _context = context;

            StorageFactory storageFactory = new AzureStorageFactory(account, ownershipContainer);
            _registration = new StorageRegistration(storageFactory);
        }

        public bool IsAuthorized
        {
            get
            {
                if (!ClaimsPrincipal.Current.Identity.IsAuthenticated)
                {
                    return false;
                }

                Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");

                if (scopeClaim == null)
                {
                    return false;
                }

                return scopeClaim.Value == "user_impersonation";
            }
        }

        public async Task<bool> IsUserAdministrator()
        {
            //return Task.FromResult(true);

            //  attempt 1

            //bool isInRole = ClaimsPrincipal.Current.IsInRole("admin");
            //return Task.FromResult(isInRole);

            //  attempt 2

            IUserFetcher user = (IUserFetcher)await GetUser();

            IPagedCollection<IDirectoryObject> pagedCollection = await user.MemberOf.ExecuteAsync();

            while (true)
            {
                foreach (IDirectoryObject directoryObject in pagedCollection.CurrentPage)
                {
                    if (directoryObject is IDirectoryRole)
                    {
                        IDirectoryRole role = (IDirectoryRole)directoryObject;
                        string roleTemplateId = role.RoleTemplateId;

                        if (roleTemplateId == "62e90394-69f5-4237-9190-012177145e10")
                        {
                            return true;
                        }
                    }
                }
                pagedCollection = await pagedCollection.GetNextPageAsync();
                if (pagedCollection == null)
                {
                    break;
                }
            }

            return false;
        }

        public Task<bool> IsTenantEnabled()
        {
            return _registration.HasTenant(GetTenantId());
        }

        public async Task AddTenant()
        {
            await _registration.AddTenant(GetTenantId());
        }

        public async Task RemoveTenant()
        {
            await _registration.RemoveTenant(GetTenantId());
        }

        async Task<ActiveDirectoryClient> GetActiveDirectoryClient()
        {
            if (_activeDirectoryClient == null)
            {
                _activeDirectoryClient = await ServiceHelpers.GetActiveDirectoryClient();
            }
            return _activeDirectoryClient;
        }

        public async Task<bool> RegistrationExists(string domain, string id)
        {
            return await _registration.Exists(new RegistrationId { Domain = domain, Id = id });
        }

        public async Task<bool> IsAuthorizedToRegistration(string domain, string id)
        {
            //IUser user = await GetUser();
            //string userObjectId = user.ObjectId;
            string userObjectId = GetName();
            return await _registration.HasOwner(new RegistrationId { Domain = domain, Id = id }, userObjectId);
        }

        public async Task AddRegistrationOwner(string domain, string id)
        {
            //IUser user = await GetUser();
            //string userObjectId = user.ObjectId;
            string userObjectId = GetName();
            await _registration.AddOwner(new RegistrationId { Domain = domain, Id = id }, userObjectId);
        }

        public async Task<bool> PackageExists(string domain, string id, string version)
        {
            return await _registration.Exists(new PackageId { Domain = domain, Id = id, Version = NuGetVersion.Parse(version) });
        }

        async Task<IUser> GetUser()
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();
            return await activeDirectoryClient.Users.GetByObjectId(GetUserId()).ExecuteAsync();
        }

        async Task<ITenantDetail> GetTenant()
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();
            return await activeDirectoryClient.TenantDetails.GetByObjectId(GetTenantId()).ExecuteAsync();
        }

        public async Task<string> GetUserName()
        {
            IUser user = await GetUser();
            return user.UserPrincipalName;
        }

        public async Task<string> GetTenantName()
        {
            ITenantDetail tenant = await GetTenant();
            return tenant.DisplayName;
        }

        public string GetTenantId()
        {
            Claim tenantClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid");
            string tenantId = (tenantClaim != null) ? tenantClaim.Value : string.Empty;
            return tenantId;
        }

        public string GetUserId()
        {
            //foreach (Claim claim in ClaimsPrincipal.Current.Claims)
            //{
            //    string issuer = claim.Issuer;
            //    string type = claim.Type;
            //    string value = claim.Value;
            //}

            Claim userClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string userId = (userClaim != null) ? userClaim.Value : string.Empty;
            return userId;
        }

        public string GetName()
        {
            Claim nameClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            string name = (nameClaim != null) ? nameClaim.Value : string.Empty;
            return name;
        }

        public async Task<IList<string>> GetDomains()
        {
            IList<string> domains = new List<string>();

            domains.Add("microsoft.com");

            /*
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            string tenantId = GetTenantId();

            ITenantDetail tenant = activeDirectoryClient.TenantDetails
                .Where(tenantDetail => tenantDetail.ObjectId.Equals(tenantId))
                .ExecuteAsync().Result.CurrentPage.FirstOrDefault();

            if (tenant == null)
            {
                throw new Exception(string.Format("unable to find tenant with object id = {0}", tenantId));
            }

            foreach (VerifiedDomain domain in tenant.VerifiedDomains)
            {
                if (domain.@default.HasValue && domain.@default.Value)
                {
                    domains.Insert(0, domain.Name);
                }
                else
                {
                    domains.Add(domain.Name);
                }
            }
            */

            return domains;
        }

        public Task<string> GetPublisherName()
        {
            Claim nameClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            string name = (nameClaim != null) ? nameClaim.Value : string.Empty;
            return Task.FromResult(name);
        }
    }
}