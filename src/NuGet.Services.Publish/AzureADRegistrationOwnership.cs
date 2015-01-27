using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.Owin;
using System;
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

        public async Task<string> GetUserName()
        {
            IUser user = await GetUser();

            return user.UserPrincipalName;
        }

        async Task<IUser> GetUser()
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();

            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            IUser user = await activeDirectoryClient.Users.GetByObjectId(userObjectID).ExecuteAsync();

            return user;
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
    }
}