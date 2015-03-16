using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public static class Artifacts
    {
        public static async Task Save(IDictionary<string, JObject> metadata, Stream packageStream, string storagePrimary, string storageContainerPackages)
        {
            string root = Guid.NewGuid().ToString();

            IList<Task<Tuple<string, Uri, Stream>>> tasks = new List<Task<Tuple<string, Uri, Stream>>>();

            using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry zipEntry in archive.Entries)
                {
                    using (Stream zipEntryStream = zipEntry.Open())
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        zipEntryStream.CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        tasks.Add(SaveFile(memoryStream, root + "/package", zipEntry.FullName, storagePrimary, storageContainerPackages));
                    }
                }
            }

            await Task.WhenAll(tasks.ToArray());

            JArray entries = new JArray();

            foreach (Task<Tuple<string, Uri, Stream>> task in tasks)
            {
                JObject entry = new JObject();

                entry["fullName"] = task.Result.Item1;
                entry["location"] = task.Result.Item2.ToString();

                entries.Add(entry);

                task.Result.Item3.Dispose();
            }

            packageStream.Seek(0, SeekOrigin.Begin);
            Tuple<string, Uri, Stream> packageEntry = await SaveFile(packageStream, root, "package.zip", storagePrimary, storageContainerPackages);

            string packageContent = packageEntry.Item2.ToString();

            JObject packageItem = new JObject
            {
                { "@type", new JArray { MetadataHelpers.GetName(Schema.DataTypes.Package, Schema.Prefixes.NuGet), MetadataHelpers.GetName(Schema.DataTypes.ZipArchive, Schema.Prefixes.NuGet) } },
                { "location", packageContent },
                { "size", packageStream.Length },
                { "hash", Utils.GenerateHash(packageStream) }
            };

            entries.Add(packageItem);

            metadata["inventory"] = new JObject
            { 
                { "entries", entries },
                { "packageContent", packageContent }
            };
        }

        static async Task<Tuple<string, Uri, Stream>> SaveFile(Stream stream, string path, string name, string storagePrimary, string storageContainerPackages)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(storageContainerPackages);

            if (await container.CreateIfNotExistsAsync())
            {
                //TODO: good for testing not so great for multi-tenant
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            string fullName = path + "/" + name;

            string contentType = MetadataHelpers.ContentTypeFromExtension(name);

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.ContentDisposition = name;
            await blob.UploadFromStreamAsync(stream);

            return Tuple.Create(name, blob.Uri, stream);
        }
    }
}
