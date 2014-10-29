using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    static class StorageTests
    {
        static async Task Test0Async()
        {
            StorageFactory factory = new FileStorageFactory(new Uri("https://tempuri.org/test"), @"c:\\data\\test");

            Console.WriteLine(factory);

            Storage storage = factory.Create();

            StorageContent content = new StringStorageContent("TEST");
            await storage.Save(new Uri(storage.BaseAddress, "doc1.txt"), content);
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }

        static async Task Test1Async()
        {
            StorageCredentials credentials = new StorageCredentials("", "");
            CloudStorageAccount account = new CloudStorageAccount(credentials, true);
            StorageFactory factory = new AzureStorageFactory(account, "ver40", "catalog", new Uri("https://tempuri.org/test"));

            Console.WriteLine(factory);

            Storage storage = factory.Create();

            StorageContent content = new StringStorageContent("TEST");
            await storage.Save(new Uri(storage.BaseAddress, "doc1.txt"), content);
        }

        public static void Test1()
        {
            Test1Async().Wait();
        }
    }
}
