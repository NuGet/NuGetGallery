using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public abstract class PublishImpl
    {
        IRegistrationOwnership _registrationOwnership;

        public PublishImpl(IRegistrationOwnership registrationOwnership)
        {
            _registrationOwnership = registrationOwnership;
        }

        protected abstract bool IsMetadataFile(string fullName);
        protected abstract JObject CreateMetadataObject(string fullname, Stream stream);
        protected abstract Uri GetItemType();

        protected virtual Task ExtractAndSavePackageArtifacts(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            return Task.FromResult(0);
        }

        protected virtual string Validate(IDictionary<string, JObject> metadata, Stream nupkgStream)
        {
            return null;
        }

        protected virtual void GenerateNuspec(IDictionary<string, JObject> metadata)
        {
        }

        public async Task CheckAccess(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsTenantEnabled())
            {
                await ServiceHelpers.WriteErrorResponse(context, "package publication has not been enabled in this tenant", HttpStatusCode.Forbidden);
                return;
            }

            string domain = context.Request.Query["domain"];

            if (string.IsNullOrWhiteSpace(domain))
            {
                await ServiceHelpers.WriteErrorResponse(context, "domain must be provided in query string", HttpStatusCode.BadRequest);
                return;
            }
            
            string id = context.Request.Query["id"];

            if (string.IsNullOrWhiteSpace(id))
            {
                await ServiceHelpers.WriteErrorResponse(context, "id must be provided in query string", HttpStatusCode.BadRequest);
                return;
            }

            IList<string> domains = await _registrationOwnership.GetDomains();
            if (!domains.Contains(domain))
            {
                await ServiceHelpers.WriteErrorResponse(context, "domain provided is not registered with the tenant", HttpStatusCode.BadRequest);
                return;
            }

            string message = string.Empty;

            if (await _registrationOwnership.RegistrationExists(domain, id))
            {
                if (!await _registrationOwnership.IsAuthorizedToRegistration(domain, id))
                {
                    string s = string.Format("User does not have access to Package Registration \"{0}\" \"{1}\" Please contact the owner(s)", domain, id);
                    await ServiceHelpers.WriteErrorResponse(context, s, HttpStatusCode.Forbidden);
                    return;
                }
                else
                {
                    message = string.Format("User has publication rights to Package Registration \"{0}\" \"{1}\"", domain, id);
                }
            }
            else
            {
                message = string.Format("Package Registration \"{0}\" \"{1}\" is available", domain, id);
            }

            JToken response = new JObject
            { 
                { "message", message }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        public async Task Upload(IOwinContext context, bool isPublic)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsTenantEnabled())
            {
                await ServiceHelpers.WriteErrorResponse(context, "package publication has not been enabled in this tenant", HttpStatusCode.Forbidden);
                return;
            }

            Stream nupkgStream = context.Request.Body;

            IDictionary<string, JObject> metadata = ExtractMetadata(nupkgStream);

            await ExtractAndSavePackageArtifacts(metadata, nupkgStream);

            string validationResponse = Validate(metadata, nupkgStream);

            if (validationResponse != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationResponse, HttpStatusCode.BadRequest);
                return;
            }

            string domain = GetDomain(metadata);
            string id = GetId(metadata);

            string error = string.Empty;

            try
            {
                if (await _registrationOwnership.RegistrationExists(domain, id))
                {
                    if (!await _registrationOwnership.IsAuthorizedToRegistration(domain, id))
                    {
                        await ServiceHelpers.WriteErrorResponse(context, "user does not have access to this registration", HttpStatusCode.Forbidden);
                        return;
                    }

                    string version = GetVersion(metadata);

                    if (await _registrationOwnership.PackageExists(domain, id, version))
                    {
                        await ServiceHelpers.WriteErrorResponse(context, "this package version already exists for this registration", HttpStatusCode.Forbidden);
                        return;
                    }
                }
                else
                {
                    await _registrationOwnership.AddRegistrationOwner(domain, id);
                }

                string nupkgName = GetNupkgName(metadata);

                Uri nupkgAddress = await SaveNupkg(nupkgStream, nupkgName);

                string publisher = await _registrationOwnership.GetUserName();

                string tenantName;
                string tenantId;

                if (isPublic)
                {
                    tenantName = "Public";
                    tenantId = "PUBLIC";
                }
                else
                {
                    tenantName = await _registrationOwnership.GetTenantName();
                    tenantId = _registrationOwnership.GetTenantId();
                }

                PublicationDetails publicationDetails = new PublicationDetails
                {
                    Published = DateTime.UtcNow,
                    UserName = await _registrationOwnership.GetUserName(),
                    UserId = _registrationOwnership.GetUserId(),
                    TenantName = tenantName,
                    TenantId = tenantId
                };

                Uri catalogAddress = await AddToCatalog(metadata["nuspec.json"], GetItemType(), nupkgAddress, nupkgStream.Length, Utils.GenerateHash(nupkgStream), publicationDetails);

                JToken response = new JObject
                { 
                    { "download", nupkgAddress.ToString() }, 
                    { "catalog", catalogAddress.ToString() }
                };

                await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);

                return;
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            await ServiceHelpers.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
        }

        public async Task GetDomains(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            IList<string> domains = await _registrationOwnership.GetDomains();

            JArray response = new JArray(domains.ToArray());

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        public async Task AddTenant(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsUserAdministrator())
            {
                await ServiceHelpers.WriteErrorResponse(context, "this operation is only permitted for administrators", HttpStatusCode.Forbidden);
                return;
            }

            await _registrationOwnership.AddTenant();

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        public async Task RemoveTenant(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.IsUserAdministrator())
            {
                await ServiceHelpers.WriteErrorResponse(context, "this operation is only permitted for administrators", HttpStatusCode.Forbidden);
                return;
            }

            await _registrationOwnership.RemoveTenant();

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        IDictionary<string, JObject> ExtractMetadata(Stream nupkgStream)
        {
            IDictionary<string, JObject> metadata = new Dictionary<string, JObject>();

            using (ZipArchive archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (IsMetadataFile(entry.FullName))
                    {
                        using (Stream stream = entry.Open())
                        {
                            metadata.Add(entry.FullName, CreateMetadataObject(entry.FullName, stream));
                        }
                    }
                }
            }

            if (!metadata.ContainsKey("nuspec.json"))
            {
                GenerateNuspec(metadata);
            }

            return metadata;
        }

        protected static string GetDomain(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];
            return nuspec["domain"].ToString().ToLowerInvariant();
        }

        protected static string GetId(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];
            return nuspec["id"].ToString().ToLowerInvariant();
        }

        protected static string GetVersion(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];
            return NuGetVersion.Parse(nuspec["version"].ToString()).ToNormalizedString();
        }

        static string GetNupkgName(IDictionary<string, JObject> metadata)
        {
            return string.Format("{0}.{1}.{2}.{3}.nupkg", 
                GetDomain(metadata),
                GetId(metadata),
                GetVersion(metadata),
                Guid.NewGuid()).ToLowerInvariant();
        }

        static Task<Uri> SaveNupkg(Stream nupkgStream, string name)
        {
            return SaveFile(nupkgStream, name, "application/octet-stream");
        }

        protected static async Task<Uri> SaveFile(Stream stream, string name, string contentType)
        {
            string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
            string storageContainerPackages = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Container.Packages") ?? "nupkgs";

            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(storageContainerPackages);
            if (await container.CreateIfNotExistsAsync())
            {
                //TODO: good for testing not so great for multi-tenant
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.ContentDisposition = name;

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            await blob.UploadFromStreamAsync(stream);

            return blob.Uri;
        }

        static async Task<Uri> AddToCatalog(JObject nuspec, Uri itemType, Uri nupkgAddress, long packageSize, string packageHash, PublicationDetails publicationDetails)
        {
            string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
            string storageContainerCatalog = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Container.Catalog") ?? "catalog";

            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            Storage storage = new AzureStorage(account, storageContainerCatalog);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
            writer.Add(new GraphCatalogItem(nuspec, itemType, nupkgAddress, packageSize, packageHash, publicationDetails));
            await writer.Commit();

            return writer.RootUri;
        }
    }
}