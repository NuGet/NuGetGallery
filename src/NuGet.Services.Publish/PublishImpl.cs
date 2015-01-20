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
        protected abstract bool IsMetadataFile(string fullName);
        protected abstract JObject CreateMetadataObject(Stream stream);
        protected abstract bool Validate(IDictionary<string, JObject> metadata, Stream nupkgStream);

        public async Task Upload(IOwinContext context, string publisher)
        {
            Stream nupkgStream = context.Request.Body;

            IDictionary<string, JObject> metadata = ExtractMetadata(nupkgStream);

            bool isValid = Validate(metadata, nupkgStream);

            if (isValid)
            {
                string nupkgName = GetNupkgName(metadata);

                Uri nupkgAddress = await SaveNupkg(nupkgStream, nupkgName);

                long packageSize = nupkgStream.Length;
                string packageHash = Utils.GenerateHash(nupkgStream);
                DateTime published = DateTime.UtcNow;

                Uri catalogAddress = await AddToCatalog(metadata, nupkgAddress, packageSize, packageHash, published, publisher);

                JToken response = new JObject
                { 
                    { "download", nupkgAddress.ToString() }, 
                    { "catalog", catalogAddress.ToString() }
                };

                await ServiceHelpers.WriteResponse(context, response, HttpStatusCode.OK);
            }
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
                            metadata.Add(entry.FullName, CreateMetadataObject(stream));
                        }
                    }
                }
            }

            return metadata;
        }

        static string GetNupkgName(IDictionary<string, JObject> metadata)
        {
            JObject nuspec = metadata["nuspec.json"];

            Uri id = nuspec["id"].ToObject<Uri>();
            string strId = id.AbsoluteUri.Substring(7);

            string version = nuspec["version"].ToString();
            string strVersion = NuGetVersion.Parse(version).ToNormalizedString();

            return MakeNupkgName(strId, strVersion);
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

        static async Task<Uri> AddToCatalog(IDictionary<string, JObject> metadata, Uri nupkgAddress, long packageSize, string packageHash, DateTime published, string publisher)
        {
            string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
            string storageContainerCatalog = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Container.Catalog") ?? "catalog";

            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            Storage storage = new AzureStorage(account, storageContainerCatalog);

            AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
            writer.Add(new GraphCatalogItem(metadata, nupkgAddress, packageSize, packageHash, published, publisher));
            await writer.Commit();

            return writer.RootUri;
        }
    }
}