using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Publish;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public static class RegistrationTests
    {
        public static async Task Test0Async()
        {
            string primary = "";

            CloudStorageAccount account = CloudStorageAccount.Parse(primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "tom");
            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "dick");
            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "harry");
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }
    }
}
