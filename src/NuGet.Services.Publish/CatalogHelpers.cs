using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public static class CatalogHelpers
    {
        public static async Task<Uri> AddToCatalog(CatalogItem catalogItem, string connectionString, string container, string catalogBaseAddress)
        {
            StorageWriteLock writeLock = new StorageWriteLock(connectionString, container);

            await writeLock.AquireAsync();

            Uri rootUri = null;

            Exception exception = null;
            try
            {
                Storage storage = CreateStorage(connectionString, container, catalogBaseAddress);

                AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage);
                writer.Add(catalogItem);
                await writer.Commit();

                rootUri = writer.RootUri;
            }
            catch (Exception e)
            {
                exception = e;
            }

            await writeLock.ReleaseAsync();

            if (exception != null)
            {
                throw exception;
            }

            return rootUri;
        }

        public static async Task<JObject> LoadFromCatalog(string catalogEntryAddress, string connectionString, string container, string catalogBaseAddress)
        {
            Storage storage = CreateStorage(connectionString, container, catalogBaseAddress);
            string json = await storage.LoadString(new Uri(catalogEntryAddress));
            return JObject.Parse(json);
        }

        static Storage CreateStorage(string connectionString, string container, string catalogBaseAddress)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);

            Storage storage;
            if (catalogBaseAddress == null)
            {
                storage = new AzureStorage(account, container);
            }
            else
            {
                string baseAddress = catalogBaseAddress.TrimEnd('/') + "/" + container;

                storage = new AzureStorage(account, container, string.Empty, new Uri(baseAddress));
            }

            return storage;
        }
    }
}