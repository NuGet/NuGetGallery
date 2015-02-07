using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
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
        bool _authorized;
        bool _initialized;
        IOwinContext _context;
        ActiveDirectoryClient _activeDirectoryClient;
        CloudStorageAccount _account;
        string _ownershipContainer;

        public StorageRegistrationOwnership(IOwinContext context, CloudStorageAccount account, string ownershipContainer)
        {
            _context = context;
            _initialized = false;
            _authorized = false;
            _account = account;
            _ownershipContainer = ownershipContainer;
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
            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);
            if (!await container.ExistsAsync())
            {
                return false;
            }
            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());
            return await ownershipBlob.ExistsAsync();
        }

        public async Task<bool> IsAuthorizedToRegistration(string id)
        {
            ActiveDirectoryClient activeDirectoryClient = await GetActiveDirectoryClient();
            IUser user = await GetUser();
            string objectId = user.ObjectId;

            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);
            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());
            string json = await ownershipBlob.DownloadTextAsync();
            JObject obj = JObject.Parse(json);

            JToken owners;
            if (obj.TryGetValue("owners", out owners))
            {
                foreach (string owner in owners)
                {
                    if (owner == objectId)
                    {
                        return true;
                    }
                }
                return false;
            }

            return true;
        }

        public async Task CreateRegistration(string id)
        {
            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);

            await container.CreateIfNotExistsAsync();

            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());

            JObject obj = new JObject { { "registration", id } };

            ownershipBlob.Properties.ContentType = "application/json";
            ownershipBlob.Properties.CacheControl = "no-store";
            await ownershipBlob.UploadTextAsync(obj.ToString());
        }

        public async Task DeleteRegistration(string id)
        {
            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);
            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());

            await ownershipBlob.DeleteIfExistsAsync();
        }

        public async Task AddRegistrationOwner(string id)
        {
            //TODO: AquireLease (or at least check etag and loop)

            User user = (User)await GetUser();

            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);
            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());
            string json = await ownershipBlob.DownloadTextAsync();
            JObject obj = JObject.Parse(json);

            JToken owners;
            if (obj.TryGetValue("owners", out owners))
            {
                ((JArray)owners).Add(user.ObjectId);
            }
            else
            {
                JArray newOwners = new JArray();
                newOwners.Add(user.ObjectId);
                obj.Add("owners", newOwners);
            }

            ownershipBlob.Properties.ContentType = "application/json";
            ownershipBlob.Properties.CacheControl = "no-store";
            await ownershipBlob.UploadTextAsync(obj.ToString());
        }

        public async Task<bool> PackageExists(string id, string version)
        {
            CloudBlobClient client = _account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(_ownershipContainer);
            CloudBlockBlob ownershipBlob = container.GetBlockBlobReference(id.ToLowerInvariant());
            string json = await ownershipBlob.DownloadTextAsync();
            JObject obj = JObject.Parse(json);

            JToken versions;
            if (obj.TryGetValue("versions", out versions))
            {
                NuGetVersion newVersion = NuGetVersion.Parse(version);

                foreach (string s in versions)
                {
                    NuGetVersion existingVersion = NuGetVersion.Parse(s);

                    if (newVersion.Equals(existingVersion))
                    {
                        return true;
                    }
                }
                return false;
            }

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

        public string GetUserId()
        {
            Claim userClaim = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            string userId = (userClaim != null) ? userClaim.Value : string.Empty;
            return userId;
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
            IList<string> registrations = new List<string>();

            return registrations;
        }
    }
}