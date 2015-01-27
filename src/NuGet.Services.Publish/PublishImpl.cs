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

            string id = context.Request.Query["id"];

            if (id == null)
            {
                await ServiceHelpers.WriteErrorResponse(context, "id must be provided in query string", HttpStatusCode.BadRequest);
                return;
            }

            string message = string.Empty;

            if (await _registrationOwnership.RegistrationExists(id))
            {
                if (!await _registrationOwnership.IsAuthorizedToRegistration(id))
                {
                    string s = string.Format("User does not have access to Package Registration \"{0}\" Please contact the owner(s)", id);
                    await ServiceHelpers.WriteErrorResponse(context, s, HttpStatusCode.Forbidden);
                    return;
                }
                else
                {
                    message = string.Format("User has publication rights to Package Registration \"{0}\"", id);
                }
            }
            else
            {
                message = string.Format("Package Registration \"{0}\" is available", id);
            }

            JToken response = new JObject
            { 
                { "message", message }
            };

            await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
        }

        public async Task Upload(IOwinContext context)
        {
            if (!_registrationOwnership.IsAuthorized)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            Stream nupkgStream = context.Request.Body;

            IDictionary<string, JObject> metadata = ExtractMetadata(nupkgStream);

            string validationResponse = Validate(metadata, nupkgStream);

            if (validationResponse != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationResponse, HttpStatusCode.BadRequest);
                return;
            }

            string id = GetId(metadata);

            bool deleteRegistrationOnError = false;

            string error = string.Empty;

            try
            {
                if (await _registrationOwnership.RegistrationExists(id))
                {
                    if (!await _registrationOwnership.IsAuthorizedToRegistration(id))
                    {
                        await ServiceHelpers.WriteErrorResponse(context, "user does not have access to this registration", HttpStatusCode.Forbidden);
                        return;
                    }
                }
                else
                {
                    await _registrationOwnership.CreateRegistration(id);

                    deleteRegistrationOnError = true;

                    await _registrationOwnership.AddRegistrationOwner(id);
                }

                string nupkgName = GetNupkgName(metadata);

                Uri nupkgAddress = await SaveNupkg(nupkgStream, nupkgName);

                long packageSize = nupkgStream.Length;
                string packageHash = Utils.GenerateHash(nupkgStream);
                DateTime published = DateTime.UtcNow;
                string publisher = await _registrationOwnership.GetUserName();

                Uri catalogAddress = await AddToCatalog(metadata["nuspec.json"], GetItemType(), nupkgAddress, packageSize, packageHash, published, publisher);

                JToken response = new JObject
                { 
                    { "download", nupkgAddress.ToString() }, 
                    { "catalog", catalogAddress.ToString() }
                };

                await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);

                deleteRegistrationOnError = false;

                return;
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (deleteRegistrationOnError)
            {
                await _registrationOwnership.DeleteRegistration(id);
            }

            await ServiceHelpers.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
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

        static string GetId(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];

            return nuspec["id"].ToString();
        }

        static string GetNupkgName(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];

            string id = nuspec["id"].ToString().ToLowerInvariant();
            string version = NuGetVersion.Parse(nuspec["version"].ToString()).ToNormalizedString();

            return MakeNupkgName(id, version);
        }

        static string MakeNupkgName(string id, string version)
        {
            return string.Format("{0}.{1}.nupkg", id, version).ToLowerInvariant();
        }

        static async Task<Uri> SaveNupkg(Stream nupkgStream, string name)
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
            blob.Properties.ContentType = "application/octet-stream";
            blob.Properties.ContentDisposition = name;

            nupkgStream.Seek(0, SeekOrigin.Begin);
            await blob.UploadFromStreamAsync(nupkgStream);

            return blob.Uri;
        }

        static async Task<Uri> AddToCatalog(JObject nuspec, Uri itemType, Uri nupkgAddress, long packageSize, string packageHash, DateTime published, string publisher)
        {
            string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
            string storageContainerCatalog = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Container.Catalog") ?? "catalog";

            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            Storage storage = new AzureStorage(account, storageContainerCatalog);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
            writer.Add(new GraphCatalogItem(nuspec, itemType, nupkgAddress, packageSize, packageHash, published, publisher));
            await writer.Commit();

            return writer.RootUri;
        }
    }
}