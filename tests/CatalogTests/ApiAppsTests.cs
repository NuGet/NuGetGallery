using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class ApiAppsTests
    {
        static string source = "https://nugetmspre.blob.core.windows.net/mscatalog/index.json";

        public static async Task Test0Async()
        {
            //  simply totals up the counts available in the pages

            CountCollector collector = new CountCollector(new Uri(source));
            await collector.Run();
            Console.WriteLine("total: {0}", collector.Total);
            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

        public static async Task Test1Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse("");

            AzureStorageFactory storageFactory = new AzureStorageFactory(account, "registration");

            string contentBaseAddress = "http://tempuri.org/content";

            CommitCollector collector = new RegistrationCatalogCollector(new Uri(source), 
                storageFactory)
            {
                ContentBaseAddress = new Uri(contentBaseAddress)
            };
            await collector.Run();

            Console.WriteLine("http requests: {0}", collector.RequestCount);
        }

    }
}
