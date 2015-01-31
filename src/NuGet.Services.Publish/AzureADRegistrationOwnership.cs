using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.Owin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public class AzureADRegistrationOwnership : IRegistrationOwnership
    {
        bool _authorized;
        bool _initialized;
        IOwinContext _context;
        ActiveDirectoryClient _activeDirectoryClient;

        public AzureADRegistrationOwnership(IOwinContext context)
        {
            _context = context;
            _initialized = false;
            _authorized = false;
        }

        public bool IsAuthorized
        {
            get
            {
                if (!_initialized)
                {
                    Claim scopeClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope");
                    _authorized = (scopeClaim != null && scopeClaim.Value == "user_impersonation");
                }
                return _authorized;
            }
        }

        async Task<ActiveDirectoryClient> GetActiveDirectoryClient()
        {
            if (_activeDirectoryClient == null)
            {
                _activeDirectoryClient = await ServiceHelpers.GetActiveDirectoryClient();
            }
            return _activeDirectoryClient;
        }

        public async Task<bool> RegistrationExists(string id)
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();
            IPagedCollection<IGroup> groups = await activeDirectoryClient.Groups.Where(gp => gp.DisplayName.Equals(id)).ExecuteAsync();
            return groups.CurrentPage.Count > 0;
        }

        public async Task<bool> IsAuthorizedToRegistration(string id)
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            IUser user = await GetUser();
            IPagedCollection<IDirectoryObject> groups = await((IUserFetcher)user).MemberOf.ExecuteAsync();

            while (true)
            {
                foreach (IDirectoryObject group in groups.CurrentPage)
                {
                    //TODO: use extension property rather than group display name registrationId
                    string groupDisplayName = ((Group)group).DisplayName;
                    if (groupDisplayName == id)
                    {
                        return true;
                    }
                }

                if (!groups.MorePagesAvailable)
                {
                    break;
                }

                groups = await groups.GetNextPageAsync();
            }

            return false;
        }

        public async Task CreateRegistration(string id)
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            Group aadGroup = new Group
            {
                DisplayName = id,
                Description = id,
                MailNickname = "nuget",
                MailEnabled = false,
                SecurityEnabled = true
            };

            await activeDirectoryClient.Groups.AddGroupAsync(aadGroup, false);
        }

        public async Task DeleteRegistration(string id)
        {
            IGroup group = await GetGroup(id);

            await group.DeleteAsync(false);
        }

        public async Task AddRegistrationOwner(string id)
        {
            Group group = (Group)await GetGroup(id);
            User user = (User)await GetUser();

            group.Members.Add((DirectoryObject)user);

            await group.UpdateAsync();
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
            Claim userClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string userId = (userClaim != null) ? userClaim.Value : string.Empty;
            return userId;
        }

        async Task<IGroup> GetGroup(string id)
        {
            //TODO: the id should be an Extension Property on the Azure-AD group not just the DisplayName

            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            IPagedCollection<IGroup> groups = await activeDirectoryClient.Groups.Where(gp => gp.DisplayName == id).ExecuteAsync();

            if (groups.CurrentPage.Count == 0)
            {
                throw new Exception(string.Format("group {0} does not exist", id));
            }

            return groups.CurrentPage.First();
        }

        public async Task<IList<string>> GetDomains()
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

            IList<string> domains = new List<string>();

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

            return domains;
        }

        public async Task<IList<string>> GetRegistrations()
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            IUser user = await GetUser();
            IPagedCollection<IDirectoryObject> groups = await((IUserFetcher)user).MemberOf.ExecuteAsync();

            IList<string> registrations = new List<string>();

            while (true)
            {
                foreach (IDirectoryObject group in groups.CurrentPage)
                {
                    registrations.Add(((Group)group).DisplayName);
                }

                if (!groups.MorePagesAvailable)
                {
                    break;
                }

                groups = await groups.GetNextPageAsync();
            }

            return registrations;
        }
    }
}