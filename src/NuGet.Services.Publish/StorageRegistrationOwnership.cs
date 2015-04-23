using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Ownership;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
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

        public StorageRegistrationOwnership(IOwinContext context, CloudStorageAccount account)
        {
            _context = context;
            _registration = new TableStorageRegistration(account);
        }

        public bool IsAuthenticated
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

        public async Task EnableTenant()
        {
            await _registration.EnableTenant(GetTenantId());
        }

        public async Task DisableTenant()
        {
            await _registration.DisableTenant(GetTenantId());
        }

        public Task<bool> HasTenantEnabled()
        {
            return _registration.HasTenantEnabled(GetTenantId());
        }

        async Task<ActiveDirectoryClient> GetActiveDirectoryClient()
        {
            if (_activeDirectoryClient == null)
            {
                _activeDirectoryClient = await ServiceHelpers.GetActiveDirectoryClient();
            }
            return _activeDirectoryClient;
        }

        public async Task<bool> HasRegistration(string ns, string id)
        {
            return await _registration.HasRegistration(new OwnershipRegistration { Namespace = ns, Id = id });
        }

        public async Task<bool> HasOwner(string ns, string id)
        {
            return await _registration.HasOwner(
                new OwnershipRegistration { Namespace = ns, Id = id },
                OwnershipOwner.Create(ClaimsPrincipal.Current));
        }

        public async Task AddVersion(string ns, string id, string version)
        {
            await _registration.AddVersion(
                new OwnershipRegistration { Namespace = ns, Id = id },
                OwnershipOwner.Create(ClaimsPrincipal.Current),
                version);
        }

        public async Task<bool> HasVersion(string ns, string id, string version)
        {
            return await _registration.HasVersion(new OwnershipRegistration { Namespace = ns, Id = id }, version);
        }

        public async Task<bool> HasNamespace(string ns)
        {
            var domains = (await GetDomains()).ToList();
            if (domains.Any())
            {
                // Check if the user can publish on the namespace
                return domains.Any(d => d.Equals(ns, StringComparison.OrdinalIgnoreCase));
            }

            // TODO: What if the user has no allowed domains. Do we return true? False? Or something else?
            // Example: foo@outlook.com makes this request and has 0 AAD namespaces. Do we return true/false here?
            // In other words: do we allow publishing to this namespace or not?
            // Went with true, so the registration records take over in further validation.
            return true;
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

        public string GetUserObjectId()
        {
            Claim identityObjectIdClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string objectId = (identityObjectIdClaim != null) ? identityObjectIdClaim.Value : string.Empty;
            return objectId;
        }

        public string GetUserId()
        {
            Claim userClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string userId = (userClaim != null) ? userClaim.Value : string.Empty;
            return userId;
        }

        public async Task<IEnumerable<string>> GetDomains()
        {
            IList<string> domains = new List<string>();

            // If no userId claim is present, we are having a Microsoft Account (MSA) requesting the domains.
            // AAD has no notion of the domains supported there, and will fail the call. Instead of going in, just return an empty list.
            var userId = GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
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
            }

            return domains;
        }

        public Task<IEnumerable<string>> GetTenants()
        {
            IList<string> tenants = new List<string>();

            tenants.Add("tenant1.com");
            tenants.Add("tenant2.com");
            tenants.Add("tenant3.com");

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

            return Task.FromResult<IEnumerable<string>>(tenants);
        }

        public Task<string> GetPublisherName()
        {
            Claim nameClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            string name = (nameClaim != null) ? nameClaim.Value : string.Empty;
            return Task.FromResult(name);
        }
    }
}